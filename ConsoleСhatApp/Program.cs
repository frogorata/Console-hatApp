using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class TerminalChatApp
{
    static bool debugMode = false;
    static string nickname = "User";

    static int serverPort;
    static string serverIP;
    static bool isServer = false;

    static List<TcpClient> clients = new List<TcpClient>();
    static Dictionary<TcpClient, string> clientNicknames = new Dictionary<TcpClient, string>();

    static void Main()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Title = "Terminal Chat v2.4 (Multiplayer)";

            while (true)
            {
                Console.WriteLine("=== Welcome to Terminal Chat v2.4 ===\n");
                Console.WriteLine("Select mode:");
                Console.WriteLine("1. Create server (multiplayer)");
                Console.WriteLine("2. Connect to server");
                Console.WriteLine($"3. {(debugMode ? "Disable" : "Enable")} debug mode");
                Console.WriteLine("4. Exit");
                Console.Write("Enter number: ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1": StartServer(); return;
                    case "2": StartClient(); return;
                    case "3": debugMode = !debugMode; Console.Clear(); break;
                    case "4": return;
                    default: Console.WriteLine("Invalid option. Try again.\n"); break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nAn error occurred: " + ex.Message);
            Console.ResetColor();
        }
    }
    static void StartClient()
    {
        Console.Clear();
        Console.Write("Your nickname: ");
        nickname = Console.ReadLine();

        Console.Write("Server IP: ");
        string ip = Console.ReadLine();

        Console.Write("Port: ");
        int port = int.TryParse(Console.ReadLine(), out var p) ? p : 8888;

        try
        {
            TcpClient client = new TcpClient(ip, port);

            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            Console.Clear();
            Console.WriteLine("Connected successfully! You can now chat.\n");

            writer.WriteLine($"/nick {nickname}");

            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        string message = reader.ReadLine();
                        if (message == null) break;

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("\n" + message);
                        Console.ResetColor();
                        Console.Write("> ");
                    }
                    catch
                    {
                        break;
                    }
                }

                Console.WriteLine("\nDisconnected from server.");
            }).Start();

            while (true)
            {
                Console.Write("> ");
                string msg = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(msg))
                    continue;

                if (msg.StartsWith("/nick "))
                {
                    nickname = msg.Substring(6).Trim();
                }

                if (msg.StartsWith("/exit"))
                {
                    client.Close();
                    Console.WriteLine("Disconnected.");
                    return;
                }

                writer.WriteLine(msg);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Connection error: " + ex.Message);
            Console.ReadLine();
        }
    }

    static void StartServer()
    {
        Console.Clear();
        Console.Write("Your nickname (server): ");
        nickname = Console.ReadLine();

        Console.Write("Port (e.g., 8888): ");
        serverPort = int.TryParse(Console.ReadLine(), out var p) ? p : 8888;
        isServer = true;

        TcpListener server = new TcpListener(IPAddress.Any, serverPort);
        server.Start();

        serverIP = GetLocalIPAddress();

        Console.Clear();
        Console.WriteLine($"\nServer created!\nName: {nickname}\nIP: {serverIP}\nPort: {serverPort}\n");

        new Thread(() =>
        {
            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                clients.Add(client);
                clientNicknames[client] = "Guest";

                Console.WriteLine($"> Connected users: {clients.Count}");

                BroadcastMessage($"[{Time()}] Server: A new user connected.", null);

                new Thread(() => HandleClient(client)).Start();
            }
        }).Start();

        while (true)
        {
            Console.Write("> ");
            string msg = Console.ReadLine();
            if (HandleCommand(msg)) continue;

            BroadcastMessage($"[{Time()}] [{nickname}]: {msg}", null);
        }
    }

    static void HandleClient(TcpClient client)
    {
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        writer.WriteLine($"[{Time()}] Server: Welcome to the chat!");

        while (true)
        {
            try
            {
                string message = reader.ReadLine();
                if (message == null) break;

                if (message.StartsWith("/nick "))
                {
                    string oldNick = clientNicknames[client];
                    string newNick = message.Substring(6).Trim();

                    clientNicknames[client] = newNick;

                    BroadcastMessage($"[{Time()}] Server: {oldNick} changed nickname to {newNick}", null);
                }
                else
                {
                    string nick = clientNicknames[client];
                    BroadcastMessage($"[{Time()}] [{nick}]: {message}", client);
                }
            }
            catch { break; }
        }

        string leftNick = clientNicknames[client];

        clients.Remove(client);
        clientNicknames.Remove(client);
        client.Close();

        BroadcastMessage($"[{Time()}] Server: {leftNick} disconnected.", null);
    }

    static void BroadcastMessage(string message, TcpClient sender)
    {
        foreach (var client in clients)
        {
            if (client == sender) continue;

            try
            {
                var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };
                writer.WriteLine(message);
            }
            catch { }
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    static bool HandleCommand(string input)
    {
        if (!input.StartsWith("/"))
            return false;

        var parts = input.Split(' ', 2);
        string command = parts[0];

        switch (command)
        {
            case "/info":
                if (!isServer)
                {
                    Console.WriteLine("Host only command.");
                    return true;
                }

                Console.WriteLine($"\nServer Name: {nickname}");
                Console.WriteLine($"IP: {serverIP}");
                Console.WriteLine($"Port: {serverPort}");
                Console.WriteLine($"Connected Users: {clients.Count}\n");
                return true;

            case "/users":
                if (!isServer)
                {
                    Console.WriteLine("Host only command.");
                    return true;
                }

                Console.WriteLine("\nConnected Users:");

                int i = 1;
                foreach (var client in clients)
                {
                    Console.WriteLine($"{i}. {clientNicknames[client]} ({client.Client.RemoteEndPoint})");
                    i++;
                }

                Console.WriteLine();
                return true;

            case "/kick":
                if (!isServer)
                {
                    Console.WriteLine("Host only command.");
                    return true;
                }

                if (parts.Length < 2 || !int.TryParse(parts[1], out int index))
                {
                    Console.WriteLine("Usage: /kick <number>");
                    return true;
                }

                index--;

                if (index < 0 || index >= clients.Count)
                {
                    Console.WriteLine("Invalid number.");
                    return true;
                }

                var clientToKick = clients[index];
                string nick = clientNicknames[clientToKick];

                var kickWriter = new StreamWriter(clientToKick.GetStream(), Encoding.UTF8) { AutoFlush = true };
                kickWriter.WriteLine($"[{Time()}] Server: You were kicked.");

                clientToKick.Close();
                clients.RemoveAt(index);
                clientNicknames.Remove(clientToKick);

                BroadcastMessage($"[{Time()}] Server: {nick} was kicked.", null);
                return true;
        }

        return false;
    }

    static string Time() => DateTime.Now.ToString("HH:mm");

    static string GetLocalIPAddress()
    {
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        }
        return "Not found";
    }
}