using NTDLS.ReliableMessaging;

namespace QueryReplyInterlacing
{
    public class Program
    {
        static readonly int _port = 36251;
        static RmServer? _server;
        static RmClient? _client;

        public static void Main(string[] args)
        {
            _server = new RmServer();
            _server.OnQueryReceived += Server_OnQueryReceived;
            _server.Start(_port);

            _client = new RmClient();
            _client.OnQueryReceived += Client_OnQueryReceived;
            _client.Connect("localhost", _port);

            var start = DateTime.UtcNow;
            int reportInterval = 2500;
            var ppsHistory = new List<double>();

            for (int packet = 0; packet < 100000; packet++)
            {
                _client.Query(new PayloadQuery() { QueryText = $"Packet:{packet}" });

                if ((packet + 1) % reportInterval == 0)
                {
                    var elapsed = DateTime.UtcNow - start;
                    double pps = (packet + 1) / elapsed.TotalSeconds;
                    ppsHistory.Add(pps);

                    Console.Write($"Packets: {packet + 1:N0}, {pps:N0}/s        \r");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Min: {ppsHistory.Min():N0}, Max: {ppsHistory.Max():N0}, Avg: {ppsHistory.Average():N0}");

            Console.WriteLine("Done sending queries.");

            _client.Disconnect();
            _server.Stop();
        }

        class PayloadQuery : IRmQuery<PayloadReply>
        {
            public string QueryText { get; set; } = string.Empty;
        }

        class PayloadReply : IRmQueryReply
        {
            public string ReplyText { get; set; } = string.Empty;
        }

        private static IRmQueryReply Server_OnQueryReceived(RmContext context, IRmPayload query)
        {
            if (query is PayloadQuery payloadQuery)
            {
                context.Query(new PayloadQuery() { QueryText = "Reply" });
                return new PayloadReply();
            }

            throw new NotImplementedException();
        }

        private static IRmQueryReply Client_OnQueryReceived(RmContext context, IRmPayload query)
        {
            if (query is PayloadQuery payloadQuery)
            {
                Thread.Sleep(100);
                return new PayloadReply();
            }

            throw new NotImplementedException();
        }
    }
}
