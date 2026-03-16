using System.Text;

namespace LatokoneAI.Common.Messaging
{
    public class IPCMessage
    {
        public static byte[] CreateMessage(int type, int count)
        {
            byte[] message = new byte[8];
            BitConverter.GetBytes(type).CopyTo(message, 0);
            BitConverter.GetBytes(count).CopyTo(message, 4);
            return message;
        }

        public static byte[] CreateMessage(int type, string data)
        {
            byte[] payload = Encoding.UTF8.GetBytes(data);
            byte[] message = new byte[4 + payload.Length];
            BitConverter.GetBytes(type).CopyTo(message, 0);
            payload.CopyTo(message, 4);
            return message;
        }

        public static byte[] CreateMessage(int type)
        {
            byte[] message = new byte[4];
            BitConverter.GetBytes(type).CopyTo(message, 0);
            return message;
        }

        public static byte[] CreateMessage(int type, byte[] buffer)
        {
            byte[] message = new byte[4 + buffer.Length];
            BitConverter.GetBytes(type).CopyTo(message, 0);
            Buffer.BlockCopy(
                buffer,                         // source array
                0,                              // source offset in bytes
                message,                        // destination array
                4,                              // destination offset in bytes
                buffer.Length                   // number of bytes to copy
            );
            return message;
        }

        public static byte[] CreateMessage(int type, float[] buffer, int count)
        {
            // Full message with id, lenght and audio data
            byte[] message = new byte[4 + 4 + count * 4];
            BitConverter.GetBytes(type).CopyTo(message, 0);
            BitConverter.GetBytes(count).CopyTo(message, 4);

            Buffer.BlockCopy(
                buffer,                         // source array
                0,                              // source offset in bytes
                message,                        // destination array
                4 * 2,                          // destination offset in bytes
                count * 4                       // number of bytes to copy
            );

            return message;
        }

        public static byte[] CreateMessage(int type, int type2, string data)
        {
            byte[] payload = Encoding.UTF8.GetBytes(data);
            byte[] message = new byte[8 + payload.Length];
            BitConverter.GetBytes(type).CopyTo(message, 0);
            BitConverter.GetBytes(type2).CopyTo(message, 4);
            payload.CopyTo(message, 8);
            return message;
        }

        public static float[] GetAudioBuffer(byte[] data)
        {
            float[] buffer = new float[(data.Length - 8) / 4];
            Buffer.BlockCopy(
                data,                           // source array
                8,                              // source offset in bytes
                buffer,                          // destination array
                0,                              // destination offset in bytes
                data.Length - 8                 // number of bytes to copy
            );
            return buffer;
        }

        public static int GetMessageType(byte[] data)
        {
            return BitConverter.ToInt32(data);
        }
    }
}
