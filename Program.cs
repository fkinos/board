using System;
using System.Text.Json;
using System.Net.Sockets;

public record Response(
    Guid identifier,
    string nickname,
    EOpcodeType messageType,
    string topic,
    string message
);

public record Request(
    string nickname,
    EOpcodeType messageType,
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
        byte[] response = Server.EncodeMessage(message);
        this.tcpClient.GetStream().Write(response, 0, response.Length);
    }
}

class Program {
    public static void Main() {
        Server server = new Server(new ServerConfig(IP: "127.0.0.1", Port: 1234));
        server.Start((Connection conn, string message, EOpcodeType messageType) => {
            Request? request = JsonSerializer.Deserialize<Request>(message);

            Response res = new Response(
                identifier: conn.Identifier,
                nickname: request?.nickname ?? conn.Identifier.ToString(),
                messageType: request?.messageType ?? EOpcodeType.Text,
                topic: "messaging",
                message: request?.message ?? ""
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
