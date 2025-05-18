const wsUri = "ws://localhost:1234";
const websocket = new WebSocket(wsUri);

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
  output.insertAdjacentElement('afterbegin', text);
}

websocket.addEventListener('open', (_event) => {
  writeToScreen("connected");
});

websocket.addEventListener('close', (_event) => {
  writeToScreen("disconnected");
});

websocket.addEventListener('message', (event) => {
  const response = JSON.parse(event.data);
  writeToScreen(`<${response.identifier.slice(0, 8)}>: ${response.message}`);
});

websocket.addEventListener('error', (event) => {
  writeToScreen(`error: ${event.data}`);
});

const button = document.querySelector("button");
const output = document.querySelector("#output");
const input = document.querySelector("input");

function doSend(message) {
  writeToScreen(`\<you\>: ${message}`);
  websocket.send(message);
}

function onClickButton() {
  const text = input.value;
  text && doSend(text);
  input.value = "";
}

button.addEventListener("click", onClickButton);
