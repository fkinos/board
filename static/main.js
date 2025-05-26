const wsUri = "ws://localhost:1234";
const websocket = new WebSocket(wsUri);

// Elementos DOM
const output = document.querySelector("#output");
const input = document.querySelector(".input");
const nicknameInput = document.querySelector(".nickname");
const button = document.querySelector("button");
const connectionStatus = document.querySelector("#connection-status");
const separator = document.querySelector('.separator');

let timerId;
const messageTimeout = 3000;

// Eventos do WebSocket
websocket.onopen = () => {
  connectionStatus.textContent = "✅ Conectado";
  connectionStatus.className = "connected";
  timerId = setTimeout(() => {
    separator.style.setProperty('display', "block");
    connectionStatus.setAttribute('aria-hidden', true);
    connectionStatus.className = "hidden";
    connectionStatus.style.setProperty('display', "none");
  }, messageTimeout);
};

websocket.onclose = () => {
  clearTimeout(timerId);
  separator.style.setProperty('display', "none");

  connectionStatus.setAttribute('aria-hidden', false);
  connectionStatus.style.setProperty('display', "block");
  connectionStatus.textContent = "❌ Desconectado";
  connectionStatus.className = "disconnected";
};

websocket.onmessage = (event) => {
  const response = JSON.parse(event.data);
  addMessage(response.nickname, response.message);
};

// Funções auxiliares
function addMessage(nickname, message) {
  const messageElement = document.createElement("div");
  messageElement.className = "message";
  messageElement.innerHTML = `<strong>${nickname}:</strong> ${message}`;
  output.appendChild(messageElement);
  output.scrollTop = output.scrollHeight;
}

function sendMessage() {
  const nickname = nicknameInput.value.trim() || "Anônimo";
  const message = input.value.trim();

  if (message) {
    websocket.send(
      JSON.stringify({
        nickname: nickname,
        message: message,
      }),
    );
    addMessage("Você", message);
    input.value = "";
  }
}

// Event Listeners
button.addEventListener("click", sendMessage);
input.addEventListener("keydown", (e) => {
  if (e.key === "Enter") sendMessage();
});
