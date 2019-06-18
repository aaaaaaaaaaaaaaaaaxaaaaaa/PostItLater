namespace PostItLater
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using Newtonsoft.Json;

    /// <summary>
    /// Starts a HTTP server to listen for requests from the tampermonkey script.
    /// </summary>
    public class RequestListener
    {
        private readonly List<Task> queue = new List<Task>();

        /// <summary>
        /// Start HTTP server.
        /// </summary>
        public void Start()
        {
            new Thread(this.Run).Start();
        }

        /// <summary>
        /// Returns indicating if requests are available to be collected.
        /// </summary>
        /// <returns>Work available.</returns>
        public bool HasWork()
        {
            lock (this.queue)
            {
                return this.queue.Count > 0;
            }
        }

        /// <summary>
        /// Get queued tasks.
        /// </summary>
        /// <returns>Array of tasks.</returns>
        public List<Task> GetWork()
        {
            List<Task> tasks;
            lock (this.queue)
            {
                tasks = new List<Task>(this.queue);
                this.queue.Clear();
            }

            return tasks;
        }

        private void Run()
        {
            var httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://localhost:4958/");
            httpListener.Start();
            while (true)
            {
                var context = httpListener.GetContext();
                var request = context.Request;
                var raw_data = new System.IO.StreamReader(request.InputStream, request.ContentEncoding).ReadToEnd();
                var data = JsonConvert.DeserializeObject<Task>(raw_data);
                Log.Info(string.Format("{0} task received.", data.type), force: true);
                lock (this.queue)
                {
                    this.queue.Add(data);
                }
            }
        }
    }
}
