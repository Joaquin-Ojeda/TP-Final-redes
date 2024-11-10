using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;

namespace WebServerSimple
{
    public class WebServer
    {
        private int port;
        private string rootDirectory;
        private string errorDirectory;

        public WebServer()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            // Leemos el archivo de configuracion y lo convierte en un objeto Config
            string configContent = File.ReadAllText("config.json");
            Config config = JsonConvert.DeserializeObject<Config>(configContent);

            port = config.Port;
            rootDirectory = config.RootDirectory;
            errorDirectory = config.ErrorDirectory;

            Console.WriteLine($"Configuración cargada: Puerto {port}, Carpeta raíz '{rootDirectory}'");
        }

        public void Start()
        {
            // Configuramos el socket para escuchar en la IP y puerto especificados
            TcpListener listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"Servidor iniciado en el puerto {port}. Esperando conexiones...");

            // Bucle para aceptar conexiones
            while (true)
            {
                // Aceptamos una conexión de cliente
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("Cliente conectado...");

                // Manejamos la peticion
                Task.Run(() => HandleRequest(client));

            }
        }

        private async Task HandleRequest(TcpClient client)
        {
            using (var stream = client.GetStream())
            {
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                string requestText = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // Extraemos el metodo y la ruta solicitada
                string[] requestLines = requestText.Split("\r\n");

                // Ejemplo de como deberia llegar GET /url?queryparam1=a&queryparam2=b HTTP/1.1
                string[] requestLine = requestLines[0].Split(" ");

                string method = requestLine[0];
                string[] urlCompleta = requestLine[1].Split("?");

                bool hasQueryParams = urlCompleta.Length > 1;

                string url = urlCompleta[0];

                Console.WriteLine($"Solicitud recibida: {requestLine[1]}");

                // Guardamos la IP
                string clientEndPoint = client.Client.RemoteEndPoint.ToString();

                // Generamos el string que se guardara en el log si corresponde
                string data = "IP: " + clientEndPoint + "\n" + "Method:" + method;
                if (hasQueryParams)
                {
                    string[] queryParams = urlCompleta[1].Split("&");
                    data += "\nQueryParams: ";
                    foreach( var param in queryParams)
                    {
                        data += param.ToString() + " ";
                    }
                }

                if (method == "GET")
                {
                    // Determinamos el archivo solicitado
                    string filePath = rootDirectory + (url == "/" ? "/index.html" : url);
                    

                    if (File.Exists(filePath))
                    {
                        SendGetResponse(stream, filePath);
                        // Se guarda el log solo si tiene query params
                        if (hasQueryParams)
                            LogData(data);
                    }
                    else
                    {
                        filePath = errorDirectory + "/error404.html";
                        SendNotFound(stream, filePath);
                    }
                }else if (method == "POST")
                {
                    // Procesamos una solicitud POST
                    int emptyLineIndex = Array.IndexOf(requestLines, "");
                    string postData = emptyLineIndex >= 0 && emptyLineIndex + 1 < requestLines.Length
                        ? requestLines[emptyLineIndex + 1] : string.Empty;

                    data += "\nRequest:" + postData;

                    // Logueamos la data
                    LogData(data);

                    // Enviamos respuesta de confirmación
                    SendPostResponse(stream);
                }


            }
            client.Close();
        }

        private void SendGetResponse(NetworkStream stream, string filePath)
        {
            // Detectamos el tipo de contenido basado en la extension
            string contentType = "text/html";  // Tipo por defecto
            string extension = Path.GetExtension(filePath).ToLower();

            if (extension == ".css")
                contentType = "text/css";
            else if (extension == ".js")
                contentType = "application/javascript";
            else if (extension == ".png")
                contentType = "image/png";
            else if (extension == ".jpg" || extension == ".jpeg")
                contentType = "image/jpeg";

            // Leemos el contenido del archivo
            byte[] content = File.ReadAllBytes(filePath);
            byte[] contentCompressed;

            // Comprimimos usando GZip
            using (var outputStream = new MemoryStream())
            {
                using (var gzip = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    gzip.Write(content, 0, content.Length);
                }
                contentCompressed = outputStream.ToArray();
            }

            // Creamos la respuesta HTTP
            string responseHeaders = "HTTP/1.1 200 OK\r\n" +
                      "Content-Encoding: gzip\r\n" +
                      $"Content-Type: {contentType}\r\n" +
                      $"Content-Length: {contentCompressed.Length}\r\n" +
                      "\r\n";

            // Enviamos los encabezados y el contenido al cliente
            byte[] headerBytes = Encoding.UTF8.GetBytes(responseHeaders);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(contentCompressed, 0, contentCompressed.Length);
        }

        private void SendNotFound(NetworkStream stream, string filePath)
        {
            // Detectamos el tipo de contenido basado en la extensión
            string contentType = "text/html";  // Tipo por defecto

            // Contenido del mensaje de error 404
            byte[] content = File.ReadAllBytes(filePath);
            byte[] contentCompressed;

            // Comprimimos usando GZip
            using (var outputStream = new MemoryStream())
            {
                using (var gzip = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    gzip.Write(content, 0, content.Length);
                }
                contentCompressed = outputStream.ToArray();
            }

            // Creamos la respuesta HTTP de error 404
            string responseHeaders = "HTTP/1.1 404 Not Found\r\n" +
                                     "Content-Encoding: gzip\r\n" +
                                     $"Content-Type: {contentType}\r\n" +
                                     $"Content-Length: {contentCompressed.Length}\r\n" +
                                     "\r\n";

            // Enviamos los encabezados y el contenido de error al cliente
            byte[] headerBytes = Encoding.UTF8.GetBytes(responseHeaders);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(contentCompressed, 0, contentCompressed.Length);
        }

        private void LogData(string data)
        {
            // Asignamos el nombre del log por dia y escribimos la data
            string logFilePath = $"log_{DateTime.Now:dd-MM-yyyy}.txt";
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Data: {data}\n";
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
        }

        private void SendPostResponse(NetworkStream stream)
        {
            string response = "HTTP/1.1 200 OK\r\n" +
                              "Content-Type: text/plain\r\n" +
                              "Content-Length: 23\r\n" +
                              "\r\n" +
                              "POST data received.\n";
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
        }
    }
}
