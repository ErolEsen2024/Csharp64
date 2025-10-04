using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CreatePRG
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string fileName = "hello.prg";
            ushort loadAddress = 0x0801;

            // ========================
            // 1) Build machine code
            // ========================
            List<byte> mcode = new List<byte>();

            int idx_msg_lo = -1;
            int idx_msg_hi = -1;
            int idx_beq_rel = -1;
            int idx_jmp_loop_lo = -1;
            int idx_jmp_loop_hi = -1;
            int idx_jmp_done_lo = -1;
            int idx_jmp_done_hi = -1;
            int idx_jmp_waitkey_lo = -1;
            int idx_jmp_waitkey_hi = -1;

            // --- Clear screen (JSR $E544)
            mcode.Add(0x20); mcode.Add(0x44); mcode.Add(0xE5);

            // --- LDX #0
            mcode.Add(0xA2); mcode.Add(0x00);

            int loopIndex = mcode.Count;

            // --- LDA message,X
            mcode.Add(0xBD);
            idx_msg_lo = mcode.Count; mcode.Add(0x00);
            idx_msg_hi = mcode.Count; mcode.Add(0x00);

            // --- BEQ done
            mcode.Add(0xF0);
            idx_beq_rel = mcode.Count; mcode.Add(0x00);

            // --- STA $0400,X
            mcode.Add(0x9D); mcode.Add(0x00); mcode.Add(0x04);

            // --- INX
            mcode.Add(0xE8);

            // --- JMP loop
            mcode.Add(0x4C);
            idx_jmp_loop_lo = mcode.Count; mcode.Add(0x00);
            idx_jmp_loop_hi = mcode.Count; mcode.Add(0x00);

            int doneIndex = mcode.Count;

            // --- JMP waitkey (we'll patch address later)
            mcode.Add(0x4C);
            idx_jmp_waitkey_lo = mcode.Count; mcode.Add(0x00);
            idx_jmp_waitkey_hi = mcode.Count; mcode.Add(0x00);

            // ========================
            // 2) Message bytes (screen codes)
            // ========================
            string text = "HELLO, WORLD!";
            byte[] screenCode = ConvertToScreenCodes(text);
            int msgIndex = mcode.Count;
            mcode.AddRange(screenCode);
            mcode.Add(0x00);

            // ========================
            // 3) Wait for key routine
            // ========================
            // Loop:
            //   JSR $FFE4   ; GETIN
            //   CMP #$00
            //   BEQ Loop
            //   RTS
            int waitKeyIndex = mcode.Count;

            // JSR $FFE4
            mcode.Add(0x20); mcode.Add(0xE4); mcode.Add(0xFF);

            // CMP #$00
            mcode.Add(0xC9); mcode.Add(0x00);

            // BEQ back to waitKeyIndex
            mcode.Add(0xF0);
            int beqWaitOffsetIndex = mcode.Count; mcode.Add(0x00); // placeholder

            // RTS
            mcode.Add(0x60);

            // Patch BEQ relative offset to loop to waitKeyIndex
            int beqOpcodeAddr_WK = 0; // patched later once base is known

            // ========================
            // 4) BASIC auto-run stub
            // ========================
            const byte TOK_SYS = 0x9E;
            bool spaceAfterSys = true;

            int digitsLen = 1;
            int machineCodeStart = 0;
            while (true)
            {
                int stubLen = 7 + digitsLen;
                machineCodeStart = loadAddress + stubLen + 2;
                int newLen = machineCodeStart.ToString().Length;
                if (newLen == digitsLen) break;
                digitsLen = newLen;
            }

            string addrStr = machineCodeStart.ToString();
            List<byte> basicStub = new List<byte>();
            int stubTotalLen = 7 + addrStr.Length;
            int pointerToEnd = loadAddress + stubTotalLen;

            // pointer to next line
            basicStub.Add((byte)(pointerToEnd & 0xFF));
            basicStub.Add((byte)(pointerToEnd >> 8));

            // line number 10
            basicStub.Add(10);
            basicStub.Add(0x00);

            // SYS token + address
            basicStub.Add(TOK_SYS);
            if (spaceAfterSys) basicStub.Add(0x20);
            basicStub.AddRange(Encoding.ASCII.GetBytes(addrStr));
            basicStub.Add(0x00);

            // terminator
            basicStub.Add(0x00);
            basicStub.Add(0x00);

            // ========================
            // 5) Patch addresses
            // ========================
            int baseAddr = loadAddress + basicStub.Count;

            // Message
            int msgAddr = baseAddr + msgIndex;
            mcode[idx_msg_lo] = (byte)(msgAddr & 0xFF);
            mcode[idx_msg_hi] = (byte)(msgAddr >> 8);

            // Loop
            int loopAddr = baseAddr + loopIndex;
            mcode[idx_jmp_loop_lo] = (byte)(loopAddr & 0xFF);
            mcode[idx_jmp_loop_hi] = (byte)(loopAddr >> 8);

            // Done → WaitKey
            int waitKeyAddr = baseAddr + waitKeyIndex;
            mcode[idx_jmp_waitkey_lo] = (byte)(waitKeyAddr & 0xFF);
            mcode[idx_jmp_waitkey_hi] = (byte)(waitKeyAddr >> 8);

            // Done BEQ
            int doneAddr = baseAddr + doneIndex;
            int beqOpcodeAddr = baseAddr + (idx_beq_rel - 1);
            int relDone = doneAddr - (beqOpcodeAddr + 2);
            mcode[idx_beq_rel] = (byte)(relDone & 0xFF);

            // WaitKey BEQ patch (back to itself)
            beqOpcodeAddr_WK = baseAddr + (beqWaitOffsetIndex - 1);
            int relWait = waitKeyAddr - (beqOpcodeAddr_WK + 2);
            mcode[beqWaitOffsetIndex] = (byte)(relWait & 0xFF);

            // ========================
            // 6) Write PRG
            // ========================
            using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((byte)(loadAddress & 0xFF));
                bw.Write((byte)(loadAddress >> 8));
                bw.Write(basicStub.ToArray());
                bw.Write(mcode.ToArray());
            }

            Console.WriteLine("Created: " + fileName);
        }

        // Converts ASCII uppercase text to C64 screen codes (default uppercase mode)
        static byte[] ConvertToScreenCodes(string input)
        {
            List<byte> output = new List<byte>();
            foreach (char c in input)
            {
                if (c >= 'A' && c <= 'Z')
                    output.Add((byte)(c - 'A' + 1)); // A=1, B=2...
                else
                    output.Add((byte)c); // basic punctuation & space same
            }
            return output.ToArray();
        }
    }
}
