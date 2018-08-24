using System.IO;
using System.Linq;
using System.Net.Sockets;

namespace CableRobot.Fins
{
    internal static class FinsDriver
    {
        private const byte MemoryAreaData = 0x82;
        private const byte MemoryAreaWork = 0xB1;
        private static readonly byte[] CommandMemoryAreaRead = new byte[] {0x01, 0x01};
        private static readonly byte[] CommandMemoryAreaWrite = new byte[] {0x01, 0x02};

        public static byte[] ReadDataCommand(Header header, ushort startAddress, ushort readCount)
            => ReadCommand(MemoryAreaData, header, startAddress, readCount);
        public static byte[] ReadWorkCommand(Header header, ushort startAddress, ushort readCount)
            => ReadCommand(MemoryAreaWork, header, startAddress, readCount);

        public static byte[] WriteDataCommand(Header header, ushort startAddress, ushort[] data)
            => WriteCommand(MemoryAreaData, header, startAddress, data);
        public static byte[] WriteWorkCommand(Header header, ushort startAddress, ushort[] data)
            => WriteCommand(MemoryAreaWork, header, startAddress, data);
        
        public static void ProcessResponse(UdpReceiveResult received, FinsResponse[] responses)
        {
            var data = received.Buffer;
            byte finishCode1 = data[12],
                finishCode2 = data[13];
            if (finishCode1 != 0 || finishCode2 != 0)
                throw new FinsException($"Failure code {finishCode1} {finishCode2}");

            var sid = data[9];

            responses[sid].PutValue(sid, ToShorts(data.Skip(14).ToArray()));
        }
        
        private static byte[] ReadCommand(byte memoryArea, Header header, ushort startAddress, ushort readCount)
        {
            var ms = new BinaryWriter(new MemoryStream());
            header.WriteTo(ms);
            ms.Write(CommandMemoryAreaRead);
            ms.Write(memoryArea);
            ms.Write((byte)(startAddress >> 8));
            ms.Write((byte)startAddress);
            ms.Write((byte)0); // Address Bit
            ms.Write((byte)(readCount >> 8));
            ms.Write((byte)readCount);
            return ((MemoryStream) ms.BaseStream).ToArray();
        }

        private static byte[] WriteCommand(byte memoryArea, Header header, ushort startAddress, ushort[] data)
        {
            var ms = new BinaryWriter(new MemoryStream());
            header.WriteTo(ms);
            ms.Write(CommandMemoryAreaWrite);
            ms.Write(memoryArea);
            ms.Write((byte)(startAddress >> 8));
            ms.Write((byte)startAddress);
            ms.Write((byte)0); // Address Bit
            ms.Write((byte)(data.Length >> 8));
            ms.Write((byte)data.Length);
            ms.Write(ToBytes(data));
            return ((MemoryStream) ms.BaseStream).ToArray();
        }

        private static ushort[] ToShorts(byte[] data)
        {
            ushort[] r =  new ushort[data.Length / 2];
            for (int i = 0; i < r.Length; i++)
            {
                r[i] = data[i * 2 + 1];
                r[i] = (ushort)(r[i] | data[i * 2] << 8);
            }
            return r;
        }

        private static byte[] ToBytes(ushort[] shorts)
        {
            byte[] r = new byte[shorts.Length * 2];
            for (int i = 0; i < shorts.Length; i++)
            {
                r[i * 2] = (byte)(shorts[i] >> 8);
                r[i * 2 + 1] = (byte)shorts[i];
            }

            return r;
        }
    }

    public class Header
    {   
        private readonly byte _icf, _rsv, _gct, 
            _destinationNetworkAddress, _destinationAddress1, _destinationAddress2, 
            _sourceNetworkAddress, _sourceAddress1, _sourceAddress2, _sid;

        public Header(byte sid, bool response)
        {
            _sid = sid; // Service id
            
            _icf = response ? (byte)0x80 : (byte)0x81;
            _rsv = 0;
            _gct = 0x02;
            _destinationNetworkAddress = 0;
            _destinationAddress1 = 0;
            _destinationAddress2 = 0;
            _sourceNetworkAddress = 0;
            _sourceAddress1 = 0x22;
            _sourceAddress2 = 0;
        }

        public void WriteTo(BinaryWriter ms)
        {
            ms.Write(
                new []
                {
                    _icf, _rsv, _gct,
                    _destinationNetworkAddress, _destinationAddress1, _destinationAddress2,
                    _sourceNetworkAddress, _sourceAddress1, _sourceAddress2,
                    _sid,
                });
        }
    }
}