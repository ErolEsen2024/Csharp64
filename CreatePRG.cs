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
            // ========================
            // BASIC + machine code file settings
            // ========================
            string fileName = "hello.prg";   // Output PRG filename
            ushort loadAddress = 0x0801;     // Standard BASIC start address ($0801)

            // ========================
            // 1) BUILD MACHINE CODE SECTION
            // ========================
            // We'll dynamically assemble 6502 machine code into this list
            List<byte> mcode = new List<byte>();

            // Indices where we will later "patch" absolute or relative addresses
            // (we don't know them yet until code size is determined)
            int idx_msg_lo = -1;
            int idx_msg_hi = -1;
            int idx_beq_rel = -1;
            int idx_jmp_loop_lo = -1;
            int idx_jmp_loop_hi = -1;
            int idx_jmp_done_lo = -1;  // (not used in this example but declared)
            int idx_jmp_done_hi = -1;  // (ditto)
            int idx_jmp_waitkey_lo = -1;
            int idx_jmp_waitkey_hi = -1;

            // --- Clear the screen using ROM routine at $E544
            //     Equivalent to: JSR $E544
            mcode.Add(0x20); mcode.Add(0x44); mcode.Add(0xE5);

            // --- Initialize X register to 0: LDX #$00
            mcode.Add(0xA2); mcode.Add(0x00);

            // Remember the loop start for later jump
            int loopIndex = mcode.Count;

            // --- LDA message,X  (load next character from message)
            mcode.Add(0xBD);
            idx_msg_lo = mcode.Count; mcode.Add(0x00); // low byte of message addr (to patch later)
            idx_msg_hi = mcode.Count; mcode.Add(0x00); // high byte of message addr

            // --- BEQ done  (if zero byte reached, branch to end)
            mcode.Add(0xF0);
            idx_beq_rel = mcode.Count; mcode.Add(0x00); // relative offset patched later

            // --- STA $0400,X (write char to screen at $0400 + X)
            mcode.Add(0x9D); mcode.Add(0x00); mcode.Add(0x04);

            // --- INX (increment X to move to next character)
            mcode.Add(0xE8);

            // --- JMP loop (go back for next character)
            mcode.Add(0x4C);
            idx_jmp_loop_lo = mcode.Count; mcode.Add(0x00); // patched later
            idx_jmp_loop_hi = mcode.Count; mcode.Add(0x00);

            // Mark where "done" code will start (after loop finishes)
            int doneIndex = mcode.Count;

            // --- JMP waitkey (jump to subroutine that waits for a keypress)
            mcode.Add(0x4C);
            idx_jmp_waitkey_lo = mcode.Count; mcode.Add(0x00); // patched later
            idx_jmp_waitkey_hi = mcode.Count; mcode.Add(0x00);

            // ========================
            // 2) MESSAGE BYTES (SCREEN CODES)
            // ========================
            string text = "HELLO, WORLD!";
            byte[] screenCode = ConvertToScreenCodes(text);
            int msgIndex = mcode.Count;
            mcode.AddRange(screenCode);
            mcode.Add(0x00); // message terminator (0 signals BEQ end)

            // ========================
            // 3) WAIT FOR KEY ROUTINE
            // ========================
            // This little routine polls GETIN ($FFE4) until a nonzero char is read.
            //   loop:
            //     JSR $FFE4   ; read keyboard buffer
            //     CMP #$00    ; compare with zero
            //     BEQ loop    ; if zero, no key yet → loop
            //     RTS         ; return to BASIC
            int waitKeyIndex = mcode.Count;

            // --- JSR $FFE4 (GETIN)
            mcode.Add(0x20); mcode.Add(0xE4); mcode.Add(0xFF);

            // --- CMP #$00
            mcode.Add(0xC9); mcode.Add(0x00);

            // --- BEQ back to waitKeyIndex (offset patched later)
            mcode.Add(0xF0);
            int beqWaitOffsetIndex = mcode.Count; mcode.Add(0x00); // placeholder

            // --- RTS
            mcode.Add(0x60);

            // We'll calculate the branch offset for BEQ once we know baseAddr
            int beqOpcodeAddr_WK = 0;

            // ========================
            // 4) BASIC AUTO-RUN STUB
            // ========================
            // This is a BASIC line 10: SYS xxxx
            // It will be placed at $0801 and tells BASIC to jump to machine code.
            const byte TOK_SYS = 0x9E;   // BASIC token for SYS
            bool spaceAfterSys = true;   // Insert a space after SYS for readability

            // We have to calculate the decimal length of the address because SYS uses ASCII digits
            int digitsLen = 1;
            int machineCodeStart = 0;
            while (true)
            {
                // 7 bytes overhead (link pointer + line number + token + terminators) + digit length
                int stubLen = 7 + digitsLen;
                machineCodeStart = loadAddress + stubLen + 2; // BASIC header itself is 2 bytes for load addr
                int newLen = machineCodeStart.ToString().Length;
                if (newLen == digitsLen) break;
                digitsLen = newLen;
            }

            // Build the SYS line
            string addrStr = machineCodeStart.ToString();
            List<byte> basicStub = new List<byte>();
            int stubTotalLen = 7 + addrStr.Length;
            int pointerToEnd = loadAddress + stubTotalLen;

            // --- Pointer to next line (or 0 to end)
            basicStub.Add((byte)(pointerToEnd & 0xFF));
            basicStub.Add((byte)(pointerToEnd >> 8));

            // --- Line number (10)
            basicStub.Add(10);
            basicStub.Add(0x00);

            // --- SYS token + (optional) space + address ASCII
            basicStub.Add(TOK_SYS);
            if (spaceAfterSys) basicStub.Add(0x20);
            basicStub.AddRange(Encoding.ASCII.GetBytes(addrStr));
            basicStub.Add(0x00); // end of line

            // --- Program terminator (end of BASIC)
            basicStub.Add(0x00);
            basicStub.Add(0x00);

            // ========================
            // 5) PATCH ADDRESSES IN MACHINE CODE
            // ========================
            // Now that BASIC stub is built, we know where machine code will start
            int baseAddr = loadAddress + basicStub.Count;

            // --- Message pointer in LDA message,X
            int msgAddr = baseAddr + msgIndex;
            mcode[idx_msg_lo] = (byte)(msgAddr & 0xFF);
            mcode[idx_msg_hi] = (byte)(msgAddr >> 8);

            // --- Loop JMP target
            int loopAddr = baseAddr + loopIndex;
            mcode[idx_jmp_loop_lo] = (byte)(loopAddr & 0xFF);
            mcode[idx_jmp_loop_hi] = (byte)(loopAddr >> 8);

            // --- Done JMP to waitKey routine
            int waitKeyAddr = baseAddr + waitKeyIndex;
            mcode[idx_jmp_waitkey_lo] = (byte)(waitKeyAddr & 0xFF);
            mcode[idx_jmp_waitkey_hi] = (byte)(waitKeyAddr >> 8);

            // --- BEQ done offset (branch if zero at end of message)
            int doneAddr = baseAddr + doneIndex;
            int beqOpcodeAddr = baseAddr + (idx_beq_rel - 1);  // F0 is one byte before offset
            int relDone = doneAddr - (beqOpcodeAddr + 2);     // 6502 relative = target - (PC+2)
            mcode[idx_beq_rel] = (byte)(relDone & 0xFF);

            // --- WaitKey BEQ offset (loop back to itself)
            beqOpcodeAddr_WK = baseAddr + (beqWaitOffsetIndex - 1);
            int relWait = waitKeyAddr - (beqOpcodeAddr_WK + 2);
            mcode[beqWaitOffsetIndex] = (byte)(relWait & 0xFF);

            // ========================
            // 6) WRITE FINAL .PRG FILE
            // ========================
            using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                // Write the 2-byte load address first (little endian)
                bw.Write((byte)(loadAddress & 0xFF));
                bw.Write((byte)(loadAddress >> 8));

                // Then write BASIC auto-run stub followed by the machine code
                bw.Write(basicStub.ToArray());
                bw.Write(mcode.ToArray());
            }

            Console.WriteLine("Created: " + fileName);
        }

        // ========================
        // HELPER: Convert ASCII text to C64 screen codes
        // ========================
        // In default uppercase mode:
        //   'A' → 1, 'B' → 2 ... 'Z' → 26
        //   Punctuation and space remain unchanged
        static byte[] ConvertToScreenCodes(string input)
        {
            List<byte> output = new List<byte>();
            foreach (char c in input)
            {
                if (c >= 'A' && c <= 'Z')
                    output.Add((byte)(c - 'A' + 1));
                else
                    output.Add((byte)c);
            }
            return output.ToArray();
        }
    }
}
