# Projeto APS 2025

Neste projeto o objetivo principal era criar uma aplicação que
utilizasse TCP/IP para possibilitar a comunicação entre vários usuários.

Para isso criamos do zero um servidor que se comunica via WebSocket,
isso possibilita uma comunicação em tempo real de todos os usuários
conectados no servidor.

Foram consultados as especifições [RFC 2616](https://datatracker.ietf.org/doc/html/rfc2616) e [RFC 6455](https://datatracker.ietf.org/doc/html/rfc6455) 
para a implementação da lógica de codificação e decodificação das mensagem enviadas e recebidas do servidor.

## Como rodar?

Para inicializar o servidor e deixa-lo pronto para escutar novas conexões 
basta rodar o seguinte comando no diretório do projeto:
```
$ dotnet run
```

Agora basta abrir o arquivo HTML encontrado em `static/index.html` para se conectar no servidor.

> É possível emular vários usuários localmente abrindo novas abas do navegador no mesmo arquivo HTML (`static/index.html`).
