using System;

namespace Diamond.Chip8.Core
{
    public static class FontSet
    {
        public static readonly byte[] Font =
        {
            0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
            0x20, 0x60, 0x20, 0x20, 0x70, // 1
            0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
            0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
            0x90, 0x90, 0xF0, 0x10, 0x10, // 4
            0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
            0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
            0xF0, 0x10, 0x20, 0x40, 0x40, // 7
            0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
            0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
            0xF0, 0x90, 0xF0, 0x90, 0x90, // A
            0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
            0xF0, 0x80, 0x80, 0x80, 0xF0, // C
            0xE0, 0x90, 0x90, 0x90, 0xE0, // D
            0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
            0xF0, 0x80, 0xF0, 0x80, 0x80, // F
        };
    }

    public static class MemoryLocations
    {
        public const ushort InterpreterStart = 0x000;
        public const ushort InterpreterEnd = 0x1FF;
        public const ushort FontStart = 0x050;
        public const ushort FontEnd = 0x0A0;
        public const ushort ProgramRomAndRamStart = 0x200;
        public const ushort ProgramRomAndRamEnd = 0xFFF;
    }

    public sealed class CoreState
    {
        public CoreState(byte[] memory, MutableState mutableState)
        {
            Memory = memory;
            MutableState = mutableState;
        }

        // Memory - 4096 bytes
        public readonly byte[] Memory;

        public MutableState MutableState;
    }

    public sealed class MutableState
    {
        public MutableState(byte[] v, ushort i, ushort pc, ushort[] stack, ushort sp, byte delayTimer, byte soundTimer, byte[] gfx, byte[] key)
        {
            V = v;
            I = i;
            PC = pc;
            Stack = stack;
            SP = sp;
            DelayTimer = delayTimer;
            SoundTimer = soundTimer;
            Gfx = gfx;
            Key = key;
        }

        // Registers - 16
        public byte[] V;

        // Index Register
        public ushort I; // 0x000 - 0xFFF

        // Program Counter
        public ushort PC; // 0x000 - 0xFFF

        // Stack - 16
        public ushort[] Stack;

        // Stack Pointer
        public ushort SP;

        // Timers
        public byte DelayTimer;
        public byte SoundTimer;

        // Graphics - 64x32
        public byte[] Gfx;

        // Keypad - 16
        public byte[] Key;
    }

    public struct OpCode
    {
        public readonly ushort FullInstruction;
        public readonly byte NibbleA;
    }

    public sealed class Chip8Core
    {
        public void Initialize()
        {
            // init stuff
        }

        public void ProcessOpcode(ushort instruction)
        {
            var a = (instruction & 0xF000) >> 12;
            var x = (instruction & 0x0F00) >> 8;
            var y = (instruction & 0x00F0) >> 4;
            var n = instruction & 0x000F;
            var nn = instruction & 0x00FF;
            var nnn = instruction & 0x0FFF;

            switch (a)
            {
                case 0x0:
                    switch (nnn)
                    {
                        case 0x0E0:
                            // Clears the screen
                            throw new NotImplementedException();
                            return;

                        case 0x0EE:
                            throw new NotImplementedException();
                            // Return
                            return;

                        default:
                            throw new NotImplementedException();
                            // Call RCA 1802 program at address NNN
                            return;
                    }

                case 0x1:
                    throw new NotImplementedException();
                    // Jump to NNN
                    return;

                case 0x2:
                    throw new NotImplementedException();
                    // Call sub at NNN
                    return;

                case 0x3:
                    throw new NotImplementedException();
                    // Skip next instruction if VX == NN
                    return;

                case 0x4:
                    // Skip next instruction if VX != NN
                    throw new NotImplementedException();
                    return;

                case 0x5:
                    // Skip next instruction if VX != VY
                    throw new NotImplementedException();
                    return;

                case 0x6:
                    // Sets VX to NN
                    throw new NotImplementedException();
                    return;

                case 0x7:
                    // Adds NN to VX (no carry flag)
                    throw new NotImplementedException();
                    return;

                case 0x8:
                    switch (n)
                    {
                        case 0x0:
                            // Set VX to value of VY
                            throw new NotImplementedException();
                            return;

                        case 0x1:
                            // Set VX to (VX or VY) bitwise
                            throw new NotImplementedException();
                            return;

                        case 0x2:
                            // Set VX to (VX and VY) bitwise
                            throw new NotImplementedException();
                            return;

                        case 0x3:
                            // VX = (VX xor VY) bitwise
                            throw new NotImplementedException();
                            return;

                        case 0x4:
                            // Adds VY to VX. VF is set to 1 when there's a carry and 0 when there isn't
                            throw new NotImplementedException();
                            return;

                        case 0x5:
                            // VY is subtracted from VX. VF is set to 0 when there's a borrow, and 1 when there isn't.
                            throw new NotImplementedException();
                            return;

                        case 0x6:
                            // Stores the least significant bit of VX in VF and then shifts VX to the right by 1.
                            throw new NotImplementedException();
                            return;

                        case 0x7:
                            // Sets VX to VY minus VX. VF is set to 0 when there's a borrow, and 1 when there isn't.
                            throw new NotImplementedException();
                            return;

                        case 0xE:
                            // Stores the most significant bit of VX in VF and then shifts VX to the left by 1.
                            throw new NotImplementedException();
                            return;

                        default:
                            throw new NotImplementedException("No matching opcode implemented.");
                    }

                case 0x9:
                    // Skips the next instruction if VX doesn't equal VY. (Usually the next instruction is a jump to skip a code block)
                    throw new NotImplementedException();
                    return;

                case 0xA:
                    // Sets I to the address NNN.
                    throw new NotImplementedException();
                    return;

                case 0xB:
                    // Jumps to the address NNN plus V0.
                    throw new NotImplementedException();
                    return;

                case 0xC:
                    // Sets VX to the result of a bitwise and operation on a random number (Typically: 0 to 255) and NN.
                    throw new NotImplementedException();
                    return;

                case 0xD:
                    // Draws a sprite at coordinate (VX, VY) that has a width of 8 pixels and a height of N pixels. Each row of 8 pixels is read as bit-coded starting from memory location I; I value doesn’t change after the execution of this instruction. As described above, VF is set to 1 if any screen pixels are flipped from set to unset when the sprite is drawn, and to 0 if that doesn’t happen
                    throw new NotImplementedException();
                    return;

                case 0xE:
                    switch (nn)
                    {
                        case 0x9E:
                            // Skips the next instruction if the key stored in VX is pressed. (Usually the next instruction is a jump to skip a code block)
                            throw new NotImplementedException();
                            return;

                        case 0xA1:
                            // Skips the next instruction if the key stored in VX isn't pressed. (Usually the next instruction is a jump to skip a code block)
                            throw new NotImplementedException();
                            return;

                        default:
                            throw new NotImplementedException("No matching opcode implemented.");
                    }

                case 0xF:
                    switch (nn)
                    {
                        case 0x07:
                            // Sets VX to the value of the delay timer.
                            throw new NotImplementedException();
                            return;

                        case 0x0A:
                            // A key press is awaited, and then stored in VX. (Blocking Operation. All instruction halted until next key event)
                            throw new NotImplementedException();
                            return;

                        case 0x15:
                            // Sets the delay timer to VX.
                            throw new NotImplementedException();
                            return;

                        case 0x18:
                            // Sets the sound timer to VX.
                            throw new NotImplementedException();
                            return;

                        case 0x1E:
                            // Adds VX to I.[
                            throw new NotImplementedException();
                            return;

                        case 0x29:
                            // Sets I to the location of the sprite for the character in VX. Characters 0-F (in hexadecimal) are represented by a 4x5 font.
                            throw new NotImplementedException();
                            return;

                        case 0x33:
                            // Stores the binary-coded decimal representation of VX, with the most significant of three digits at the address in I, the middle digit at I plus 1, and the least significant digit at I plus 2. (In other words, take the decimal representation of VX, place the hundreds digit in memory at location in I, the tens digit at location I+1, and the ones digit at location I+2.)
                            throw new NotImplementedException();
                            return;

                        case 0x55:
                            // Stores V0 to VX (including VX) in memory starting at address I. The offset from I is increased by 1 for each value written, but I itself is left unmodified.
                            throw new NotImplementedException();
                            return;

                        case 0x65:
                            // Fills V0 to VX (including VX) with values from memory starting at address I. The offset from I is increased by 1 for each value written, but I itself is left unmodified.
                            throw new NotImplementedException();
                            return;

                        default:
                            throw new NotImplementedException("No matching opcode implemented.");
                    }
            }
        }

        public void Tick()
        {
            var currentOpcode = (ushort) ((State.Memory[State.MutableState.PC] << 8) | State.Memory[State.MutableState.PC + 1]);

        }

        public CoreState State;
    }
}
