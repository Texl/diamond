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

    public class CoreState
    {
        public byte[] Memory = new byte[4096];
        public byte[] V = new byte[16];
        public ushort I; // 0x000 - 0xFFF
        public ushort PC; // 0x000 - 0xFFF
        public ushort CurrentOpcode;
        public ushort[] Stack = new ushort[16];
        public ushort SP;

        // Timers
        public byte DelayTimer;
        public byte SoundTimer;

        // IO
        public byte[] Gfx = new byte[64 * 32];
        public byte[] Key = new byte[16];
    }

    public static class CoreOps
    {

    }
}
