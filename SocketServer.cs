using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Newtonsoft.Json;
//using Claims;
using System.IO;
using System;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
//using System.Transactions;

namespace CTRL
{
    public class SocketServer
    {
        private int count = 0;
        //DumptoText DTT;
        public WebSocket webSocket;
        private string projectDirectory = "";

        public SocketServer(string projectDirectory)
        {
            //this.DTT = DTT;
            this.projectDirectory = projectDirectory;
        }

        //### Starting the server        
        // Using HttpListener is reasonably straightforward. Start the listener and run a loop that receives and processes incoming WebSocket connections.
        // Each iteration of the loop "asynchronously waits" for the next incoming request using the `GetContextAsync` extension method (defined below).             
        // If the request is for a WebSocket connection then pass it on to `ProcessRequest` - otherwise set the status code to 400 (bad request). 
        public async void Start(string listenerPrefix)
        {

            try
            {
                HttpListener listener = new HttpListener();
                listener.Prefixes.Add(listenerPrefix);
                listener.Start();
                Console.WriteLine("Listening...on:" + listenerPrefix);

                while (true)
                {
                    HttpListenerContext listenerContext = await listener.GetContextAsync();
                    Console.WriteLine("Connected");
                    if (listenerContext.Request.IsWebSocketRequest)
                    {
                        Console.WriteLine("Is a websocketrequest");
                        ProcessRequest(listenerContext);
                    }
                    else
                    {
                        Console.WriteLine("Is not a websocketrequest");
                        listenerContext.Response.StatusCode = 400;
                        listenerContext.Response.Close();
                    }
                }
            }
            catch (Exception e)
            {
                //DTT.AddWrite(e.ToString());
                //DTT.Write();
                //delete urlacl url=https://+:80/MyUri
                //netsh http delete urlacl url=https://+:80/MyUri
                try
                {
                    System.Diagnostics.Process.Start("netsh.exe", "http delete urlacl url=http://10.0.0.207:24277/wsDemo/");
                    //http://+:80/wsDemo/
                }
                catch (Exception e2)
                {
                    //DTT.AddWrite(e2.ToString());
                    //DTT.AddWrite("ERROR WHEN TRYING TO UNREGISTER URL WHEN RETARTING SERVER - exiting application");
                    //DTT.Write();
                    System.Environment.Exit(0);
                }

            }



        }

        //### Accepting WebSocket connections
        // Calling `AcceptWebSocketAsync` on the `HttpListenerContext` will accept the WebSocket connection, sending the required 101 response to the client
        // and return an instance of `WebSocketContext`. This class captures relevant information available at the time of the request and is a read-only 
        // type - you cannot perform any actual IO operations such as sending or receiving using the `WebSocketContext`. These operations can be 
        // performed by accessing the `System.Net.WebSocket` instance via the `WebSocketContext.WebSocket` property.        
        private async void ProcessRequest(HttpListenerContext listenerContext)
        {

            WebSocketContext webSocketContext = null;
            try
            {
                // When calling `AcceptWebSocketAsync` the negotiated subprotocol must be specified. This sample assumes that no subprotocol 
                // was requested. 
                webSocketContext = await listenerContext.AcceptWebSocketAsync(subProtocol: null);
                Interlocked.Increment(ref count);
                Console.WriteLine("Processed: {0}", count);
            }
            catch (Exception e)
            {
                // The upgrade process failed somehow. For simplicity lets assume it was a failure on the part of the server and indicate this using 500.
                listenerContext.Response.StatusCode = 500;
                listenerContext.Response.Close();
                Console.WriteLine("Exception: {0}", e);
                return;
            }

            webSocket = webSocketContext.WebSocket;


            try
            {
                //### Receiving
                // Define a receive buffer to hold data received on the WebSocket connection. The buffer will be reused as we only need to hold on to the data
                // long enough to send it back to the sender.
                //byte[] receiveBuffer = new byte[16000];

                // While the WebSocket connection remains open run a simple loop that receives data and sends it back.
                while (webSocket.State == WebSocketState.Open)
                {
                    // The first step is to begin a receive operation on the WebSocket. `ReceiveAsync` takes two parameters:
                    //
                    // * An `ArraySegment` to write the received data to. 
                    // * A cancellation token. In this example we are not using any timeouts so we use `CancellationToken.None`.
                    //
                    // `ReceiveAsync` returns a `Task<WebSocketReceiveResult>`. The `WebSocketReceiveResult` provides information on the receive operation that was just 
                    // completed, such as:                
                    //
                    // * `WebSocketReceiveResult.MessageType` - What type of data was received and written to the provided buffer. Was it binary, utf8, or a close message?                
                    // * `WebSocketReceiveResult.Count` - How many bytes were read?                
                    // * `WebSocketReceiveResult.EndOfMessage` - Have we finished reading the data for this message or is there more coming?
                    while (true)
                    {
                        Console.WriteLine("Receiving data");
                        WebSocketReceiveResult result;
                        //WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                        int bufferSize = 1000;
                        var buffer = new byte[bufferSize];
                        var offset = 0;
                        var free = buffer.Length;
                        while (true)
                        {
                            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, free), CancellationToken.None);
                            offset += result.Count;
                            free -= result.Count;
                            if (result.EndOfMessage) break;
                            if (free == 0)
                            {
                                // No free space
                                // Resize the outgoing buffer
                                var newSize = buffer.Length + bufferSize;
                                // Check if the new size exceeds a limit
                                // It should suit the data it receives
                                // This limit however has a max value of 2 billion bytes (2 GB)
                                if (newSize > 2000000000)
                                {
                                    throw new Exception("Maximum size exceeded");
                                }
                                var newBuffer = new byte[newSize];
                                Array.Copy(buffer, 0, newBuffer, 0, offset);
                                buffer = newBuffer;
                                free = buffer.Length - offset;
                            }
                        }
                        Console.WriteLine("Received data:");
                        Console.Write("Data byte size: " + buffer.Length);


                        if (Encoding.ASCII.GetString(buffer).Contains("__ping__"))
                        {
                            //send pong, reset
                            Console.WriteLine("Sending pong back");
                            string s = "__pong__";
                            //int bufferSize2 = 1000;
                            var buffer2 = Encoding.ASCII.GetBytes(s);
                            var offset2 = 0;
                            var free2 = buffer2.Length;
                            System.ArraySegment<byte> spong = new ArraySegment<byte>(buffer2, offset2, free2);//Encoding.UTF8.GetBytes(s);
                            await webSocket.SendAsync(spong, WebSocketMessageType.Binary, result.EndOfMessage, CancellationToken.None);
                            continue;
                        }

                        // The WebSocket protocol defines a close handshake that allows a party to send a close frame when they wish to gracefully shut down the connection.
                        // The party on the other end can complete the close handshake by sending back a close frame.
                        //
                        // If we received a close frame then lets participate in the handshake by sending a close frame back. This is achieved by calling `CloseAsync`. 
                        // `CloseAsync` will also terminate the underlying TCP connection once the close handshake is complete.
                        //
                        // The WebSocket protocol defines different status codes that can be sent as part of a close frame and also allows a close message to be sent. 
                        // If we are just responding to the client's request to close we can just use `WebSocketCloseStatus.NormalClosure` and omit the close message.
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                            Console.WriteLine("Closing commmunication received from client websocketmessagetype.close");
                            break;
                        }
                        // This echo server can't handle text frames so if we receive any we close the connection with an appropriate status code and message.
                        //else if (receiveResult.MessageType == WebSocketMessageType.Text)
                        //{
                        //    await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Cannot accept text frame", CancellationToken.None);
                        //    Console.WriteLine("Closing commmunication received from client websocketmessagetype.text");
                        //    break;
                        //}
                        // Otherwise we must have received binary data. Send it back by calling `SendAsync`. Note the use of the `EndOfMessage` flag on the receive result. This
                        // means that if this echo server is sent one continuous stream of binary data (with EndOfMessage always false) it will just stream back the same thing.
                        // If binary messages are received then the same binary messages are sent back.
                        else
                        {
                            string s = Encoding.ASCII.GetString(buffer);


                            //var sz = JsonConvert.SerializeObject(s);
                            //Console.WriteLine("JSON data received:");
                            //Console.WriteLine(sz);
                            //s = Encoding.ASCII.GetString(s);
                            //Console.WriteLine("Sending data back and closing");
                            //await webSocket.SendAsync(new ArraySegment<byte>(receiveBuffer, 0, receiveResult.Count), WebSocketMessageType.Binary, receiveResult.EndOfMessage, CancellationToken.None);

                            //rnk.ExecuteWork();

                            //break;




                        }

                        // The echo operation is complete. The loop will resume and `ReceiveAsync` is called again to wait for the next data frame.
                    }



                }
            }
            catch (Exception e)
            {
                // Just log any exceptions to the console. Pretty much any exception that occurs when calling `SendAsync`/`ReceiveAsync`/`CloseAsync` is unrecoverable in that it will abort the connection and leave the `WebSocket` instance in an unusable state.
                Console.WriteLine("Exception: {0}", e);
                //DTT.AddWrite(e.ToString());
                //DTT.Write();
            }
            finally
            {
                //DTT.Write();
                // Clean up by disposing the WebSocket once it is closed/aborted.
                if (webSocket != null)
                    webSocket.Dispose();
            }
        }

    }
}

