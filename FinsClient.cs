using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CableRobot.Fins
{
    /// <summary>
    /// Allows to write and read PLC memory
    /// </summary>
    public class FinsClient : IDisposable
    {   
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _cancellationToken;
        private readonly UdpClient _udpClient;
        private readonly FinsResponse[] _responses = new FinsResponse[256];
        private readonly Thread _readerThread;
        private readonly object _lockObject = new object();
        private byte _sid;
        
        public FinsClient(IPEndPoint remoteIpEndPoint)
        {
            _udpClient = new UdpClient();
            _udpClient.Connect(remoteIpEndPoint);
            
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            
            _readerThread = new Thread(ReadWorker);
            _readerThread.Start();

            for (int i = 0; i < _responses.Length; i++)
                _responses[i] = new FinsResponse((byte)i, null);

            Timeout = TimeSpan.FromSeconds(2);
        }
        
        /// <summary>
        /// Gets or sets response timeout
        /// </summary>
        public TimeSpan Timeout { get; set; }
        
        public void Close()
        {
            _cancellationTokenSource.Cancel();
            _readerThread.Join();
        }

        /// <summary>
        /// Syncroniously reads specified number of ushorts starting from specified address in data memory
        /// </summary>
        /// <param name="startAddress">Address to start to read from</param>
        /// <param name="count">Number of ushorts to read</param>
        /// <returns>Read data</returns>
        public ushort[] ReadData(ushort startAddress, ushort count)
        {
            var sid = IncrementSid();
            var cmd = FinsDriver.ReadDataCommand(new Header(sid, true), startAddress, count);
            return Read(sid, cmd);
        }

        /// <summary>
        /// Syncroniously reads specified number of ushorts starting from specified address in work memory
        /// </summary>
        /// <param name="startAddress">Address to start to read from</param>
        /// <param name="count">Number of ushorts to read</param>
        /// <returns>Read data</returns>
        public ushort[] ReadWork(ushort startAddress, ushort count)
        {
            var sid = IncrementSid();
            var cmd = FinsDriver.ReadWorkCommand(new Header(sid, true), startAddress, count);
            return Read(sid, cmd);
        }

        /// <summary>
        /// Syncroniously writes specified data to specified address of data memory
        /// </summary>
        /// <param name="startAddress">Address to start write to</param>
        /// <param name="data">Data to write</param>
        public void WriteData(ushort startAddress, ushort[] data)
        {
            var sid = IncrementSid();
            var cmd = FinsDriver.WriteDataCommand(new Header(sid, true), startAddress, data);
            Write(sid, cmd);
        }

        /// <summary>
        /// Syncroniously writes specified data to specified address of work memory
        /// </summary>
        /// <param name="startAddress">Address to start write to</param>
        /// <param name="data">Data to write</param>
        public void WriteWork(ushort startAddress, ushort[] data)
        {
            var sid = IncrementSid();
            var cmd = FinsDriver.WriteWorkCommand(new Header(sid, true), startAddress, data);
            Write(sid, cmd);
        }

        /// <summary>
        /// Asynchronously reads specified number of ushorts starting from specified address in data memory
        /// </summary>
        /// <param name="startAddress">Address to start to read from</param>
        /// <param name="count">Number of ushorts to read</param>
        /// <returns>Read data</returns>
        public async Task<ushort[]> ReadDataAsync(ushort startAddress, ushort count)
        {
            var sid = IncrementSid();
            var cmd = FinsDriver.ReadDataCommand(new Header(sid, true), startAddress, count);
            return (await CommandAsync(sid, cmd)).Data;
        }

        /// <summary>
        /// Asynchronously writes specified data to specified address of data memory
        /// </summary>
        /// <param name="startAddress">Address to start to write to</param>
        /// <param name="data">Data to write</param>
        public async Task WriteDataAsync(ushort startAddress, ushort[] data)
        {
            var sid = IncrementSid();
            var cmd = FinsDriver.WriteDataCommand(new Header(sid, true), startAddress, data);
            await CommandAsync(sid, cmd);
        }

        /// <summary>
        /// Writes specified data to specified address of data memory without
        /// </summary>
        /// <param name="startAddress">Address to start to read from</param>
        /// <param name="count">Number of ushorts to read</param>
        public void WriteDataNoResponse(ushort startAddress, ushort[] data)
        {
            var sid = IncrementSid();
            var cmd = FinsDriver.WriteDataCommand(new Header(sid, false), startAddress, data);
            _udpClient.SendAsync(cmd, cmd.Length);
        }
        
        private byte IncrementSid()
        {
            byte sid;
            lock (_lockObject)
            {
                _sid++;
                sid = _sid;
            }

            _responses[sid].Reset();
            return sid;
        }
        
        private ushort[] Read(byte sid, byte[] cmd)
        {
            if (_udpClient.Send(cmd, cmd.Length) != cmd.Length)
                throw new Exception();
            if (!_responses[sid].WaitEvent.WaitOne(Timeout))
                throw new TimeoutException();
            return _responses[sid].Data;
        }

        private void Write(byte sid, byte[] cmd)
        {
            if (_udpClient.Send(cmd, cmd.Length) != cmd.Length)
                throw new Exception();
            if (!_responses[sid].WaitEvent.WaitOne(Timeout))
                throw new TimeoutException();
        }

        private async Task<FinsResponse> CommandAsync(byte sid, byte[] cmd)
        {
            if (await _udpClient.SendAsync(cmd, cmd.Length) != cmd.Length)
                throw new Exception();
            if (!_responses[sid].WaitEvent.WaitOne(Timeout))
                throw new TimeoutException();
            return _responses[sid];
        }
        
        private void ReadWorker()
        {
            try
            {
                while (true)
                {
                    var task = _udpClient.ReceiveAsync();
                    task.Wait(_cancellationToken);
                    
                    if (task.IsFaulted)
                        throw new AggregateException(task.Exception);

                    FinsDriver.ProcessResponse(task.Result, _responses);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
        
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _readerThread?.Join();
            _udpClient?.Dispose();
        }
    }
}
