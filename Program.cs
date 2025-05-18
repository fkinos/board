using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Sockets;

class Program {
    public static void Main() {
        Server server = new Server(new ServerConfig(IP: "127.0.0.1", Port: 1234));
        server.Start((Connection conn, string message, Server.EOpcodeType messageType) => {
            Response res = new Response(
                identifier: conn.Identifier,
                messageType: messageType,
                topic: "messaging",
                message: message
            );
            string jsonResponse = JsonSerializer.Serialize(res);

            foreach ((Guid id, Connection c) in server.Connections) {
                if (conn.Identifier == id) continue;
                c.Write(jsonResponse);
            }

            return true;
        });
    }
}

public record Response(
    Guid identifier,
    Server.EOpcodeType messageType,
    string topic,
    string message
);

public class Connection {
    private TcpClient tcpClient;

    private Guid identifier;
    public Guid Identifier => identifier;

    public Connection(TcpClient tcpClient) {
        this.tcpClient = tcpClient;
        this.identifier = Guid.NewGuid();
    }

    public void Write(string message) {
        byte[] response = Server.SendMessage(message);
        this.tcpClient.GetStream().Write(response, 0, response.Length);
    }
}

public record ServerConfig(
    string IP,
    int Port
);

public class Server {
    public enum EOpcodeType : int {
        // Denotes a continuation code
        Fragment = 0,
        // Denotes a text code
        Text = 1,
        // Denotes a binary code
        Binary = 2,
        // Denotes a closed connection
        ClosedConnection = 8,
        // Denotes a ping
        Ping = 9,
        // Denotes a pong
        Pong = 10
    }

    private ServerConfig serverConfig;
    public ServerConfig ServerConfig => serverConfig;

    private Dictionary<Guid, Connection> connections;
    public Dictionary<Guid, Connection> Connections => connections;

    public Server(ServerConfig serverConfig) {
        this.serverConfig = serverConfig;
        this.connections = new Dictionary<Guid, Connection>();
    }

    public void Start(Func<Connection, string, EOpcodeType, bool> handler) {
        TcpListener tcpServer = new TcpListener(
            IPAddress.Parse(this.serverConfig.IP),
            this.serverConfig.Port
        );

        tcpServer.Start();

        Console.WriteLine(
            "@ server started at {0}",
            this.serverConfig.IP + ":" + this.serverConfig.Port.ToString()
        );

        while (true) {
            TcpClient client = tcpServer.AcceptTcpClient();
            Task.Run(() => HandleClient(client, handler));
        }
    }

    private void HandleClient(
        TcpClient tcpClient,
        Func<Connection, string, EOpcodeType, bool> handler
    ) {
        Connection conn = new Connection(tcpClient);

        if (!this.connections.ContainsKey(conn.Identifier))
            this.connections.Add(conn.Identifier, conn);

        Console.WriteLine("@ new connection {0}", conn.Identifier);

        NetworkStream stream = tcpClient.GetStream();

        while (true) {
            while (!stream.DataAvailable);

            byte[] clientBytes = new byte[tcpClient.Available];
            stream.Read(clientBytes, 0, clientBytes.Length);
            string clientMessage = Encoding.UTF8.GetString(clientBytes);

            // If it's a GET request we upgrade the connection to support WebSockets
            if (Regex.IsMatch(clientMessage, "^GET", RegexOptions.IgnoreCase)) {
                byte[] response = Server.GenerateHandShakeResponse(clientMessage);
                stream.Write(response, 0, response.Length);
                continue;
            }

            (string decodedMessage, EOpcodeType messageType) = Server.HandleMessage(clientBytes);
            handler(conn, decodedMessage, messageType);
        }
    }

    private static (string decodedMessage, EOpcodeType messageType) HandleMessage(byte[] clientBytes) {
        // Base Framing Protocol
        // https://datatracker.ietf.org/doc/html/rfc6455#section-5.2

        // FIN: 1 bit
        //
        // Indicates that this is the final fragment in a message.
        // The first fragment MAY also be the final fragment.
        //
        bool fin = (clientBytes[0] & 0b10000000) != 0;

        // RSV1, RSV2, RSV3: 1 bit each
        //
        // MUST be 0 unless an extension is negotiated that defines meanings
        // for non-zero values.  If a nonzero value is received and none of
        // the negotiated extensions defines the meaning of such a nonzero
        // value, the receiving endpoint MUST Fail the WebSocket Connection.
        //
        bool rsv1 = (clientBytes[0] & 0b01000000) == 0;
        bool rsv2 = (clientBytes[0] & 0b00100000) == 0;
        bool rsv3 = (clientBytes[0] & 0b00010000) == 0;

        if ((rsv1 && rsv2 && rsv3) == false) {
            throw new InvalidOperationException("RSV1, RSV2, and RSV3 must be 0.");
        }

        // Opcode: 4 bits
        //
        // Defines the interpretation of the "Payload data".  If an unknown
        // opcode is received, the receiving endpoint MUST Fail the
        // WebSocket Connection.  The following values are defined.
        //
        // * %x0 denotes a continuation frame
        // * %x1 denotes a text frame
        // * %x2 denotes a binary frame
        // * %x3-7 are reserved for further non-control frames
        // * %x8 denotes a connection close
        // * %x9 denotes a ping
        // * %xA denotes a pong
        // * %xB-F are reserved for further control frames
        //
        int opcode =  clientBytes[0] & 0b00001111; // expecting %x1 - text frame

        // if (opcode != 0x1) {
        //     throw new InvalidOperationException("Only handling text frames for now.");
        // }

        // Mask: 1 bit
        //
        // Defines whether the "Payload data" is masked.  If set to 1, a
        // masking key is present in masking-key, and this is used to unmask
        // the "Payload data" as per Section 5.3.  All frames sent from
        // client to server have this bit set to 1.
        //
        bool mask  = (clientBytes[1] & 0b10000000) != 0;

        if (!mask) {
            throw new InvalidOperationException("Mask bit is not set.");
        }

        // Payload length: 7 bits, 7+16 bits, or 7+64 bits
        //
        // The length of the "Payload data", in bytes:
        //
        // * if 0-125, that is the payload length.
        //
        // * If 126, the following 2 bytes interpreted as a
        // 16-bit unsigned integer are the payload length.
        //
        // * If 127, the following 8 bytes interpreted as a
        // 64-bit unsigned integer (the most significant bit MUST be 0) are the payload length.
        //
        // Multibyte length quantities are expressed in network byte order.
        //
        // Note that in all cases, the minimal number of bytes MUST be used to encode
        // the length, for example, the length of a 124-byte-long string
        // can't be encoded as the sequence 126, 0, 124.  The payload length
        // is the length of the "Extension data" + the length of the
        // "Application data".
        // The length of the "Extension data" may be zero,
        // in which case the payload length is the length of the "Application data".
        //
        ulong payloadLength = clientBytes[1] & (ulong)0b01111111;
        (ulong offset, ulong messageLength) = Server.DetermineMessageLength(payloadLength, clientBytes.Skip(2));

        byte[] decodedMessageBuffer = new byte[messageLength];

        // The masking key is a 32-bit value chosen at random by the client.
        byte[] maskingKey = new byte[4] {
            clientBytes[offset + 0],
            clientBytes[offset + 1],
            clientBytes[offset + 2],
            clientBytes[offset + 3]
        };

        offset += 4;

        // To convert masked data into unmasked data, or vice versa, the following algorithm is applied.
        // The same algorithm applies regardless of the direction of the translation,
        // e.g., the same steps are applied to mask the data as to unmask the data.
        //
        // Octet i of the transformed data ("transformed-octet-i") is the XOR of
        // octet i of the original data ("original-octet-i") with octet at index
        // i modulo 4 of the masking key ("masking-key-octet-j"):
        //      j                   = i MOD 4
        //      transformed-octet-i = original-octet-i XOR masking-key-octet-j
        //
        for (ulong i = 0; i < messageLength; ++i)
            decodedMessageBuffer[i] = (byte)(clientBytes[offset + i] ^ maskingKey[i % 4]);

        string decodedMessage = Encoding.UTF8.GetString(decodedMessageBuffer);

        return (decodedMessage: decodedMessage, messageType: (EOpcodeType)opcode);
    }

    public static byte[] SendMessage(
        string message,
        EOpcodeType opcode = EOpcodeType.Text
    ) {
        byte[] response;
        byte[] bytesRaw = Encoding.Default.GetBytes(message);
        byte[] frame = new byte[10];

        int indexStartRawData = -1;
        int length = bytesRaw.Length;

        frame[0] = (byte)(128 + (int)opcode);

        if (length <= 125) {
            frame[1] = (byte)length;
            indexStartRawData = 2;
        } else if (length >= 126 && length <= 65535) {
            frame[1] = (byte)126;
            frame[2] = (byte)((length >> 8) & 255);
            frame[3] = (byte)(length & 255);
            indexStartRawData = 4;
        } else {
            frame[1] = (byte)127;
            frame[2] = (byte)((length >> 56) & 255);
            frame[3] = (byte)((length >> 48) & 255);
            frame[4] = (byte)((length >> 40) & 255);
            frame[5] = (byte)((length >> 32) & 255);
            frame[6] = (byte)((length >> 24) & 255);
            frame[7] = (byte)((length >> 16) & 255);
            frame[8] = (byte)((length >> 8) & 255);
            frame[9] = (byte)(length & 255);

            indexStartRawData = 10;
        }

        response = new byte[indexStartRawData + length];

        int i, reponseIdx = 0;

        for (i = 0; i < indexStartRawData; i++) {
            response[reponseIdx] = frame[i];
            reponseIdx++;
        }

        for (i = 0; i < length; i++) {
            response[reponseIdx] = bytesRaw[i];
            reponseIdx++;
        }

        return response;
    }

    private static (ulong offset, ulong messageLength) DetermineMessageLength(
        ulong payloadLength,
        IEnumerable<byte> clientBytes
    ) {
        if (payloadLength >= 0 && payloadLength <= 125) return (offset: 2, messageLength: payloadLength);

        if (payloadLength == 126) {
            ulong messageLength = BitConverter.ToUInt16(clientBytes.Take(2).Reverse().ToArray(), 0);
            return (offset: 4, messageLength: messageLength);
        }

        if (payloadLength == 127) {
            ulong messageLength = BitConverter.ToUInt16(clientBytes.Take(8).Reverse().ToArray(), 0);
            return (offset: 10, messageLength: messageLength);
        }

        throw new InvalidOperationException("Payload length must be 0-125, 126, or 127.");
    }

    private static byte[] GenerateHandShakeResponse(string message) {
        // https://datatracker.ietf.org/doc/html/rfc2616#section-2.2
        //
        // HTTP/1.1 defines the sequence CR LF as the end-of-line marker for all
        // protocol elements except the entity-body.
        //
        string carriageReturnCharacter = "\r";
        string lineFeedCharacter = "\n";
        string endOfLineMarker = carriageReturnCharacter + lineFeedCharacter;

        // https://datatracker.ietf.org/doc/html/rfc6455#section-1.3
        //
        // To prove that the handshake was received, the server has to take two
        // pieces of information and combine them to form a response.  The first
        // piece of information comes from the |Sec-WebSocket-Key| header field
        // in the client handshake:
        //
        //      Sec-WebSocket-Key
        //
        // For this header field, the server has to take the value (as present
        // in the header field, e.g., the base64-encoded [RFC4648] version minus
        // any leading and trailing whitespace) and concatenate this with the
        // Globally Unique Identifier (GUID, [RFC4122]) "258EAFA5-E914-47DA-
        // 95CA-C5AB0DC85B11" in string form, which is unlikely to be used by
        // network endpoints that do not understand the WebSocket Protocol.  A
        // SHA-1 hash (160 bits) [FIPS.180-3], base64-encoded (see Section 4 of
        // [RFC4648]), of this concatenation is then returned in the server's
        // handshake.
        //
        string globallyUniqueIdentifier = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        string swk = Regex.Match(message, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
        string swkAndSalt = swk + globallyUniqueIdentifier;
        byte[] swkAndSaltSha1 = System.Security.Cryptography.SHA1.Create()
            .ComputeHash(Encoding.UTF8.GetBytes(swkAndSalt));
        string acceptedHash = Convert.ToBase64String(swkAndSaltSha1);

        byte[] response = Encoding.UTF8.GetBytes(
            "HTTP/1.1 101 Switching Protocols" + endOfLineMarker
            + "Connection: Upgrade" + endOfLineMarker
            + "Upgrade: websocket" + endOfLineMarker
            + "Sec-WebSocket-Accept: " + acceptedHash + endOfLineMarker
            + endOfLineMarker
        );

        return response;
    }
}
