const wsUri = "ws://localhost:1234";
const websocket = new WebSocket(wsUri);

let connectionState = {
  connected: false,
};

const MessageType = {
  // Denotes a continuation code
  Fragment: 0,
  // Denotes a text code
  Text: 1,
  // Denotes a binary code
  Binary: 2,
  // Denotes a closed connection
  ClosedConnection: 8,
  // Denotes a ping
  Ping: 9,
  // Denotes a pong
  Pong: 10
}

const getKeyByValue = (object, value) => {
  return Object.keys(object).find(key => object[key] === value);
}

function writeToScreen(message) {
  const text = document.createElement('p');
  text.innerText = message;
  output.appendChild(text);
}

websocket.addEventListener('open', (_event) => {
  connectionState.connected = true;
});

websocket.addEventListener('close', (_event) => {
  connectionState.connected = false;
});

websocket.addEventListener('message', (event) => {
  const response = JSON.parse(event.data);

  console.info('@ response', event, response);

  writeToScreen(`<${response.nickname ? response.nickname : response.identifier.slice(0, 8)}>: ${response.message}`);
});

websocket.addEventListener('error', (event) => {
  console.error(event);
});

const button = document.querySelector("button");
const output = document.querySelector("#output");
const input = document.querySelector(".input");
const nicknameInput = document.querySelector(".nickname");

function doSend(connectionState, message) {
  if (!connectionState.connected) {
    alert("Not connected to the server");
    return;
  }

  const nickname = nicknameInput.value;

  const request = {
    nickname: nickname ? nickname : '',
    messageType: MessageType.Text,
    message: message,
  }

  console.info('@ request', request);

  websocket.send(JSON.stringify(request));

  writeToScreen(`\<${request.nickname ? request.nickname : 'you'}>: ${request.message}`);
}

function onClickButton() {
  const text = input.value;
  text && doSend(connectionState, text);
  input.value = "";
}

button.addEventListener("click", onClickButton);
input.addEventListener("keydown", (event) => {
  const keyCode = event.code;

  if (keyCode !== 'Enter') return;

  const text = input.value;
  text && doSend(connectionState, text);
  input.value = '';
})
