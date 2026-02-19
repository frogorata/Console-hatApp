using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class TerminalChatApp
{
    static bool debugMode = false;

    static string nickname = "User";

    static bool isServer = false;
    static int serverPort = 8888;
    static string serverIP = "Not found";

    static readonly object clientsLock = new object();

    static readonly List<TcpClient> clients = new List<TcpClient>();
    static readonly Dictionary<TcpClient, string> clientNicknames = new Dictionary<TcpClient, string>();

    static readonly object consoleLock = new object();

    static void Main()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Title = "Terminal Chat v2.5 (Multiplayer)";

            while (true)
            {
                WriteLineSafe("=== Welcome to Terminal Chat v2.5 ===\n");
                WriteLineSafe("Select mode:");
                WriteLineSafe("1. Create server (multiplayer)");
                WriteLineSafe("2. Connect to server");
                WriteLineSafe($"3. {(debugMode ? "Disable" : "Enable")} debug mode");
                WriteLineSafe("4. Exit");
                WriteSafe("Enter number: ");

                string choice = Console.ReadLine()?.Trim();

                switch (choice)
                {
                    case "1": StartServer(); return;
                    case "2": StartClient(); return;
                    case "3": debugMode = !debugMode; Console.Clear(); break;
                    case "4": return;
                    default: WriteLineSafe("Invalid option. Try again.\n"); break;
                }
            }
        }
        catch (Exception ex)
        {
            WriteLineSafe($"\nAn error occurred: {ex.Message}", ConsoleColor.Red);
        }
        finally
        {
            WriteLineSafe("\nPress Enter to exit...");
            Console.ReadLine();
        }
    }

    static void StartServer()
    {
        Console.Clear();
        WriteSafe("Your nickname (server): ");
        nickname = (Console.ReadLine() ?? "Host").Trim();
        if (string.IsNullOrWhiteSpace(nickname)) nickname = "Host";

        WriteSafe("Port (e.g., 8888): ");
        serverPort = int.TryParse(Console.ReadLine(), out var p) ? p : 8888;

        isServer = true;

        TcpListener server = new TcpListener(IPAddress.Any, serverPort);
        server.Start();

        serverIP = GetLocalIPAddress();
        Console.Clear();
        WriteLineSafe($"\nServer created!\nName: {nickname}\nIP: {serverIP}\nPort: {serverPort}\n");

        new Thread(() =>
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = server.AcceptTcpClient();
                }
                catch
                {
                    return;
                }

                lock (clientsLock)
                {
                    clients.Add(client);
                    clientNicknames[client] = "Guest";
                }

                PrintDebug($"New client connected: {client.Client.RemoteEndPoint}");
                WriteLineSafe($"> Connected users: {GetClientCount()}");

                BroadcastMessage($"[{Time()}] Server: A user has connected.", sender: null);

                new Thread(() => HandleClient(client)) { IsBackground = true }.Start();
            }
        })
        { IsBackground = true }.Start();

        while (true)
        {
            WriteSafe("> ");
            string msg = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(msg))
                continue;

            if (HandleCommand(msg, writer: null))
                continue;

            BroadcastMessage($"[{Time()}] [{nickname}]: {msg}", sender: null);
        }
    }

    static void HandleClient(TcpClient client)
    {
        string endpoint = SafeEndpoint(client);

        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            writer.WriteLine($"[{Time()}] Server: Welcome to the chat!");
            writer.WriteLine($"[{Time()}] Server: Type /help to see available commands.");

            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;

                line = line.TrimEnd();
                if (line.Length == 0) continue;

                if (line.StartsWith("/nick ", StringComparison.OrdinalIgnoreCase))
                {
                    string newNick = line.Substring(6).Trim();
                    if (string.IsNullOrWhiteSpace(newNick))
                    {
                        writer.WriteLine($"[{Time()}] Server: Usage: /nick <name>");
                        continue;
                    }

                    string oldNick;
                    lock (clientsLock)
                    {
                        oldNick = clientNicknames.TryGetValue(client, out var n) ? n : "Guest";
                        clientNicknames[client] = newNick;
                    }

                    BroadcastMessage($"[{Time()}] Server: {oldNick} changed nickname to {newNick}.", sender: null);
                    continue;
                }

                string nickNow = GetNickname(client);
                BroadcastMessage($"[{Time()}] [{nickNow}]: {line}", sender: client);
            }
        }
        catch
        {
            // ignore :D
        }
        finally
        {
            string leftNick = GetNickname(client);

            lock (clientsLock)
            {
                clients.Remove(client);
                clientNicknames.Remove(client);
            }

            try { client.Close(); } catch { }

            PrintDebug($"Client disconnected: {endpoint}");
            BroadcastMessage($"[{Time()}] Server: {leftNick} disconnected.", sender: null);
            WriteLineSafe($"> Connected users: {GetClientCount()}");
        }
    }

    static void BroadcastMessage(string message, TcpClient sender)
    {
        List<TcpClient> snapshot;
        lock (clientsLock)
        {
            snapshot = clients.ToList();
        }

        foreach (var c in snapshot)
        {
            if (c == sender) continue;

            try
            {
                var w = new StreamWriter(c.GetStream(), Encoding.UTF8) { AutoFlush = true };
                w.WriteLine(message);
            }
            catch
            {
                // ignore broken sockets
            }
        }

        if (isServer)
        {
            WriteLineSafe(message, ConsoleColor.Cyan, reprintPrompt: true);
        }
    }

    static void StartClient()
    {
        Console.Clear();
        WriteSafe("Your nickname: ");
        nickname = (Console.ReadLine() ?? "User").Trim();
        if (string.IsNullOrWhiteSpace(nickname)) nickname = "User";

        WriteSafe("Server IP: ");
        string ip = (Console.ReadLine() ?? "").Trim();

        WriteSafe("Port: ");
        int port = int.TryParse(Console.ReadLine(), out var p) ? p : 8888;

        bool stopLoading = false;
        Thread loadingThread = new Thread(() =>
        {
            string[] dots = { ".", "..", "..." };
            int i = 0;
            while (!stopLoading)
            {
                lock (consoleLock)
                {
                    Console.Write($"\rConnecting to server{dots[i]}   ");
                }
                Thread.Sleep(450);
                i = (i + 1) % dots.Length;
            }
        })
        { IsBackground = true };

        loadingThread.Start();

        try
        {
            TcpClient client = new TcpClient(ip, port);

            stopLoading = true;
            loadingThread.Join();
            lock (consoleLock) Console.WriteLine();

            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            Console.Clear();
            WriteLineSafe("Connected successfully! You can now chat.\n");

            writer.WriteLine($"/nick {nickname}");

            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        string message = reader.ReadLine();
                        if (message == null) break;

                        WriteLineSafe(message, ConsoleColor.Cyan, reprintPrompt: true);
                    }
                    catch
                    {
                        break;
                    }
                }

                WriteLineSafe("\nDisconnected from server.", ConsoleColor.Red);
                Environment.Exit(0);
            })
            { IsBackground = true }.Start();

            while (true)
            {
                WriteSafe("> ");
                string msg = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(msg))
                    continue;

                if (HandleCommand(msg, writer))
                    continue;

                writer.WriteLine(msg);

                WriteLineSafe($"[{Time()}] [{nickname}]: {msg}", ConsoleColor.Green);
            }
        }
        catch (Exception ex)
        {
            stopLoading = true;
            try { loadingThread.Join(); } catch { }
            WriteLineSafe("\nConnection error: " + ex.Message, ConsoleColor.Red);
            Console.ReadLine();
        }
    }

    static bool HandleCommand(string input, StreamWriter writer = null)
    {
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith("/"))
            return false;

        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1].Trim() : "";

        switch (cmd)
        {
            case "/exit":
                WriteLineSafe("You left the chat.");
                Environment.Exit(0);
                return true;

            case "/clear":
                Console.Clear();
                return true;

            case "/help":
                WriteLineSafe(@"
/exit   - leave the chat
/nick   - change nickname
/clear  - clear screen
/info   - show server info (host only)
/users  - list connected users (server only)
/kick   - kick user by number (server only)
/help   - show commands
".TrimEnd());
                return true;

            case "/nick":
                if (string.IsNullOrWhiteSpace(arg))
                {
                    WriteLineSafe("Usage: /nick <name>");
                    return true;
                }

                string old = nickname;
                nickname = arg;

                if (!isServer && writer != null)
                    writer.WriteLine($"/nick {nickname}");

                if (isServer)
                    BroadcastMessage($"[{Time()}] Server: Host changed nickname from {old} to {nickname}.", sender: null);

                WriteLineSafe($"Nickname changed to: {nickname}");
                return true;

            case "/info":
                if (!isServer)
                {
                    WriteLineSafe("This command is only available on the server (host).");
                    return true;
                }

                WriteLineSafe($@"
--- Server Information ---
Server Name: {nickname}
IP Address: {serverIP}
Port: {serverPort}
Connected Users: {GetClientCount()}
--------------------------".Trim());
                return true;

            case "/users":
                if (!isServer)
                {
                    WriteLineSafe("This command is only available on the server (host).");
                    return true;
                }

                PrintUsers();
                return true;

            case "/kick":
                if (!isServer)
                {
                    WriteLineSafe("This command is only available on the server (host).");
                    return true;
                }

                if (!int.TryParse(arg, out int number))
                {
                    WriteLineSafe("Usage: /kick <user number>");
                    return true;
                }

                KickByNumber(number);
                return true;

            default:
                return false;
        }
    }

    static void PrintUsers()
    {
        List<(string Nick, string Endpoint)> list;

        lock (clientsLock)
        {
            list = clients
                .Select(c => (Nick: clientNicknames.TryGetValue(c, out var n) ? n : "Guest",
                             Endpoint: SafeEndpoint(c)))
                .ToList();
        }

        WriteLineSafe("\n--- Connected Users ---");
        if (list.Count == 0)
        {
            WriteLineSafe("No users connected.");
        }
        else
        {
            for (int i = 0; i < list.Count; i++)
                WriteLineSafe($"{i + 1}. {list[i].Nick} ({list[i].Endpoint})");
        }
        WriteLineSafe("-----------------------\n");
    }

    static void KickByNumber(int userNumber)
    {
        if (userNumber <= 0)
        {
            WriteLineSafe("Invalid user number.");
            return;
        }

        TcpClient target = null;
        string targetNick = "Guest";
        string endpoint = "";

        lock (clientsLock)
        {
            int idx = userNumber - 1;
            if (idx < 0 || idx >= clients.Count)
            {
                WriteLineSafe("Invalid user number.");
                return;
            }

            target = clients[idx];
            endpoint = SafeEndpoint(target);
            if (clientNicknames.TryGetValue(target, out var n))
                targetNick = n;
        }

        try
        {
            var w = new StreamWriter(target.GetStream(), Encoding.UTF8) { AutoFlush = true };
            w.WriteLine($"[{Time()}] Server: You have been kicked.");
        }
        catch { }

        try { target.Close(); } catch { }

        lock (clientsLock)
        {
            clients.Remove(target);
            clientNicknames.Remove(target);
        }

        BroadcastMessage($"[{Time()}] Server: {targetNick} ({endpoint}) was kicked.", sender: null);
        WriteLineSafe($"> Connected users: {GetClientCount()}");
    }


    static int GetClientCount()
    {
        lock (clientsLock) return clients.Count;
    }

    static string GetNickname(TcpClient client)
    {
        lock (clientsLock)
        {
            return clientNicknames.TryGetValue(client, out var n) ? n : "Guest";
        }
    }

    static string SafeEndpoint(TcpClient client)
    {
        try { return client?.Client?.RemoteEndPoint?.ToString() ?? "unknown"; }
        catch { return "unknown"; }
    }

    static void PrintDebug(string msg)
    {
        if (!debugMode) return;
        WriteLineSafe("[DEBUG] " + msg, ConsoleColor.DarkGray);
    }

    static string Time() => DateTime.Now.ToString("HH:mm");

    static string GetLocalIPAddress()
    {
        try
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch { }
        return "Not found";
    }

    static void WriteSafe(string text)
    {
        lock (consoleLock)
        {
            Console.Write(text);
        }
    }

    static void WriteLineSafe(string text, ConsoleColor? color = null, bool reprintPrompt = false)
    {
        lock (consoleLock)
        {
            if (color.HasValue)
            {
                var old = Console.ForegroundColor;
                Console.ForegroundColor = color.Value;
                Console.WriteLine(text);
                Console.ForegroundColor = old;
            }
            else
            {
                Console.WriteLine(text);
            }

            if (reprintPrompt)
                Console.Write("> ");
        }
    }
}
