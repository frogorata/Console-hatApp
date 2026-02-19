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
    static List<TcpClient> clients = new List<TcpClient>();

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
        finally
        {
            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }
    }

    static void StartServer()
    {
        Console.Clear();
        Console.Write("Your nickname (server): ");
        nickname = Console.ReadLine();

        Console.Write("Port (e.g., 8888): ");
        int port = int.TryParse(Console.ReadLine(), out var p) ? p : 8888;

        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();

        string ipAddress = GetLocalIPAddress();
        Console.Clear();
        Console.WriteLine($"\nServer created!\nName: {nickname}\nIP: {ipAddress}\nПорт: {port}\n");

        new Thread(() =>
        {
            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                clients.Add(client);
                PrintDebug("New client connected.");
                Console.WriteLine($"> Connected users: {clients.Count}");
                BroadcastMessage($"[{Time()}] Server: A user has connected.", client);
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
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n" + message);
                Console.ResetColor();
                BroadcastMessage(message, client);
            }
            catch { break; }
        }
        clients.Remove(client);
        client.Close();
    }

    static void BroadcastMessage(string message, TcpClient sender)
    {
        foreach (var client in clients)
        {
            if (client == sender) continue;
            var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(message);
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

        bool stopLoading = false;
        Thread loadingThread = new Thread(() =>
        {
            string[] dots = { ".", "..", "..." };
            int i = 0;
            while (!stopLoading)
            {
                Console.Write($"\rConnecting to server{dots[i]}   ");
                Thread.Sleep(500);
                i = (i + 1) % dots.Length;
            }
        });
        loadingThread.Start();

        try
        {
            TcpClient client = new TcpClient(ip, port);
            stopLoading = true;
            loadingThread.Join();

            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            Console.Clear();
            Console.WriteLine("Connected successfully! You can now chat.\n");

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
                    catch { break; }
                }
            }).Start();

            while (true)
            {
                Console.Write("> ");
                string msg = Console.ReadLine();
                if (HandleCommand(msg, writer)) continue;
                string fullMsg = $"[{Time()}] [{nickname}]: {msg}";
                writer.WriteLine(fullMsg);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n" + fullMsg);
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            stopLoading = true;
            loadingThread.Join();
            Console.WriteLine("Connection error: " + ex.Message);
            Console.ReadLine();
        }
    }

    static bool HandleCommand(string input, StreamWriter writer = null)
    {
        if (input.StartsWith("/exit"))
        {
            Console.WriteLine("You left the chat.");
            Environment.Exit(0);
        }
        else if (input.StartsWith("/clear"))
        {
            Console.Clear();
            return true;
        }
        else if (input.StartsWith("/nick "))
        {
            nickname = input.Substring(6).Trim();
            Console.WriteLine($"Nickname changed to: {nickname}");
            return true;
        }
        else if (input.StartsWith("/help"))
        {
            Console.WriteLine("\n/exit — leave the chat\n/nick <name> — change nickname\n/clear — clear screen\n/help — show commands\n");
            return true;
        }
        return false;
    }

    static void PrintDebug(string msg)
    {
        if (debugMode)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("[DEBUG] " + msg);
            Console.ResetColor();
        }
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