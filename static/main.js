const wsUri = "ws://localhost:1234";
const websocket = new WebSocket(wsUri);

function writeToScreen(message) {
  const text = document.createElement('p');
  text.innerText = message;
  output.insertAdjacentElement('afterbegin', text);
}

websocket.addEventListener('open', (_event) => {
  writeToScreen("connected:");
});

websocket.addEventListener('close', (_event) => {
  writeToScreen("disconnected:");
});

websocket.addEventListener('message', (event) => {
  writeToScreen(`<server>: ${event.data}`);
});

websocket.addEventListener('error', (event) => {
  writeToScreen(`error: ${event.data}`);
});

const button = document.querySelector("button");
const output = document.querySelector("#output");
const input = document.querySelector("input");

function doSend(message) {
  writeToScreen(`\<client\>: ${message}`);
  websocket.send(message);
}

function onClickButton() {
  const text = input.value;
  text && doSend(text);
  input.value = "";
}

button.addEventListener("click", onClickButton);
