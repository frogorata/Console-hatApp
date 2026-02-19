# ConsoleChatApp (Terminal TCP Chat)

A terminal-based TCP chat written in C# (.NET).
Run as a server (host) or connect as a client. Built for quick LAN / direct IP testing.

## Features

- Multiplayer server mode (accepts multiple TCP clients)
- Client mode (connect by IP + port)
- UTF-8 input/output (supports non-ASCII text)
- Basic command system
- Server-side nickname tracking
- Thread-safe console output (reduces mixed/overlapping prints)
- Host tools: list users and kick

## Commands

Available in chat:

- `/exit` — leave the chat
- `/nick <name>` — change nickname
- `/clear` — clear screen
- `/info` — show server info (host only)
- `/users` — list connected users (server only)
- `/kick <number>` — kick user by number (server only)
- `/help` — show commands

## Requirements

- .NET SDK 6+ (recommended)
- Windows / Linux / macOS terminal

## Build & Run

From the repository root:

```bash
dotnet build
cd ConsoleChatApp
dotnet run
```

## Usage

### Start a server (host)

1. Run the app
2. Choose `1. Create server (multiplayer)`
3. Enter a nickname and port (default: 8888)
4. Share the shown IP and port with clients

Example:
- IP: `192.168.1.10`
- Port: `8888`

### Connect as a client

1. Run the app
2. Choose `2. Connect to server`
3. Enter your nickname
4. Enter server IP and port

Local testing (same PC):
- IP: `127.0.0.1`
- Port: `8888`

## Notes

- If connecting from another PC, make sure the server machine allows inbound connections on the chosen port (Firewall).
- If you are behind NAT, this is intended primarily for LAN/direct testing unless you configure port forwarding.

## Troubleshooting

### Connection refused / timeout

- Check IP and port
- Ensure the server is running
- Allow the port through the firewall
- Make sure both devices are on the same network (for LAN testing)

### Messages look broken

- UTF-8 is enabled by default in the app, but ensure your terminal supports UTF-8 fonts/encoding

## License

No license is currently specified.
