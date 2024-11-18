using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System;

class WebSocketServer
{
    static async Task Main(string[] args)
    {
        string uri = "http://localhost:5928/broadcast/";
        HttpListener httpListener = new HttpListener();
        httpListener.Prefixes.Add(uri);
        httpListener.Start();
        Console.WriteLine("WebSocket сервер запущен");

        ConcurrentBag<WebSocket> clients = new ConcurrentBag<WebSocket>();

        while (true)
        {
            HttpListenerContext context = await httpListener.GetContextAsync();

            if (context.Request.IsWebSocketRequest)
            {
                HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
                WebSocket client = wsContext.WebSocket;
                clients.Add(client);
                Console.WriteLine("Клиент подключился");
                
                _ = Task.Run(() => HandleClient(client, clients));
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    static async Task HandleClient(WebSocket client, ConcurrentBag<WebSocket> clients)
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (client.State == WebSocketState.Open)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("Клиент отключился");
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Закрытие соединения", CancellationToken.None);
                    clients.TryTake(out client);
                }
                else
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Получено сообщение: {message}");

                    await BroadcastMessage(message, clients, client);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
        finally
        {
            if (client.State == WebSocketState.Open)
            {
                await client.CloseAsync(WebSocketCloseStatus.InternalServerError, "Ошибка сервера", CancellationToken.None);
            }
        }
    }

    static async Task BroadcastMessage(string message, ConcurrentBag<WebSocket> clients, WebSocket sender)
    {
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);

        foreach (var client in clients)
        {
            if (client != sender && client.State == WebSocketState.Open)
            {
                try
                {
                    await client.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    Console.WriteLine("Ошибка отправки сообщения клиенту");
                }
            }
        }
    }
}