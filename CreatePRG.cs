using System;
using System.IO;
using System.Text;

namespace CreatePRG
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // The output file name for the generated Commodore 64 PRG file.
            string fileName = "hello.prg";

            // ============================
            // BASIC PROGRAM HEADER VALUES
            // ============================

            // The standard C64 BASIC program load address.
            // $0801 (2049 decimal) is where BASIC programs typically start in memory.
            // This is the first two bytes of the PRG file (little endian).
            ushort loadAddress = 0x0801;

            // The address in memory of the next BASIC line after this one.
            // This is how Commodore BASIC internally chains lines together.
            // If the address is 0, BASIC knows it's the end of the program.
            // Here 0x0817 is hardcoded as the pointer to the next line,
            // which happens to be just after the line we're creating.
            ushort nextAddress = 0x0817;

            // The BASIC line number for the program. (0x000A = decimal 10)
            // When you list the program on the C64, this will appear as:
            //   10 PRINT "HELLO, WORLD!"
            ushort lineNumber = 0x000A;

            // BASIC token for the PRINT command is $99 (153 decimal).
            // Commodore BASIC uses tokens for keywords instead of storing them as text.
            byte byteToken = 0x99;

            // ================================
            // CREATE AND WRITE THE PRG FILE
            // ================================

            // Create a new file stream for the output file, with write access.
            // Using "using" ensures the stream is properly closed and disposed
            // even if an exception occurs.
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // ----------------------------
                // 1) WRITE LOAD ADDRESS
                // ----------------------------
                // Commodore PRG files start with a 2-byte little-endian
                // memory address indicating where to load the file in memory.
                // Here: $0801 → bytes [0x01, 0x08].
                bw.Write((byte)(loadAddress & 0xFF));  // Low byte
                bw.Write((byte)(loadAddress >> 8));    // High byte

                // ----------------------------
                // 2) WRITE BASIC LINE POINTER
                // ----------------------------
                // Each BASIC line starts with a 2-byte pointer to the address
                // of the next line in memory. This allows the BASIC interpreter
                // to traverse the program line by line.
                bw.Write((byte)(nextAddress & 0xFF));  // Low byte of next line
                bw.Write((byte)(nextAddress >> 8));    // High byte of next line

                // ----------------------------
                // 3) WRITE LINE NUMBER
                // ----------------------------
                // Next comes the 2-byte line number, little endian.
                // This is what the user sees in LIST.
                bw.Write((byte)(lineNumber & 0xFF));   // Low byte of line number
                bw.Write((byte)(lineNumber >> 8));     // High byte of line number

                // ----------------------------
                // 4) WRITE BASIC TOKENS + TEXT
                // ----------------------------
                // First, the PRINT token ($99)
                bw.Write(byteToken);

                // Then we append the BASIC code after the PRINT keyword.
                // Here, BuildBasicLine returns the PETSCII-encoded bytes for:
                //   " \"HELLO, WORLD!\""
                // including a null terminator (0x00) at the end of the line.
                bw.Write(BuildBasicLine(" \"HELLO, WORLD!\""));

                // ----------------------------
                // 5) WRITE PROGRAM TERMINATOR
                // ----------------------------
                // The end of a Commodore BASIC program is indicated by a line pointer
                // of 0x0000 (two zero bytes). This is how BASIC knows it's the end.
                bw.Write((byte)0x00);
                bw.Write((byte)0x00);
            }

            // Notify the user that the file was successfully created.
            Console.WriteLine("C64 PRG file '" + fileName + "' created.");
        }

        // ================================================
        // Helper function to build the BASIC code segment
        // ================================================
        static byte[] BuildBasicLine(string code)
        {
            // Convert the BASIC code text to PETSCII bytes.
            // For simple ASCII letters and punctuation, ASCII is close enough,
            // but a true implementation would need a full PETSCII conversion.
            // The .ToUpper() call ensures the output matches how C64 BASIC stores keywords.
            byte[] codeBytes = Encoding.ASCII.GetBytes(code.ToUpper());

            // Structure of a BASIC line (after the line number):
            //   [token(s) + code...][0x00 terminator]
            //
            // The next-line pointer and line number are handled outside this function.
            // We just return the tokenized line data ending with a null terminator.
            int totalLength = codeBytes.Length + 1;  // +1 for the 0x00 terminator
            byte[] line = new byte[totalLength];

            // Copy the code bytes into the line array.
            Array.Copy(codeBytes, 0, line, 0, codeBytes.Length);

            // Append the null terminator (0x00) at the end.
            line[codeBytes.Length] = 0x00;

            // Return the finished BASIC line bytes.
            return line;
        }
    }
}
