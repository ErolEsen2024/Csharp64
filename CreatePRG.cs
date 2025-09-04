using System;
using System.IO;
using System.Text;
namespace CreatePRG
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string fileName = "hello.prg";
            // C64 BASIC load address
            ushort loadAddress = 0x0801;
            ushort nextAddress = 0x0817;
            ushort lineNumber = 0x000A;
            byte byteToken = 0x99; // PRINT
            // Create the BASIC program as bytes
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // Write load address (little-endian)
                bw.Write((byte)(loadAddress & 0xFF));
                bw.Write((byte)(loadAddress >> 8));
                bw.Write((byte)(nextAddress & 0xFF));
                bw.Write((byte)(nextAddress >> 8));
                bw.Write((byte)(lineNumber & 0xFF));
                bw.Write((byte)(lineNumber >> 8));
                bw.Write(byteToken); // PRINT
                bw.Write(BuildBasicLine(" \"HELLO, WORLD!\""));
                // End of BASIC program (0x00)
                bw.Write((byte)0x00);
                bw.Write((byte)0x00);
            }
            Console.WriteLine("C64 PRG file '"+fileName+"' created.");
        }
        // Builds a BASIC line in PRG format
        static byte[] BuildBasicLine(string code)
        {
            // Convert the BASIC code to PETSCII (ASCII mostly works for simple strings)
            byte[] codeBytes = Encoding.ASCII.GetBytes(code.ToUpper());
            // Line structure: [next line ptr][line number][tokens+code][0x00]
            // We'll calculate length and then fill in next line pointer later
            int totalLength = codeBytes.Length + 1;
            byte[] line = new byte[totalLength];
            // Code bytes
            Array.Copy(codeBytes, 0, line,0, codeBytes.Length);
            // Null terminator
            line[codeBytes.Length] = 0x00;
            // Return line (caller will compute the next line pointer as needed)
            return line;
        }
    }
}
