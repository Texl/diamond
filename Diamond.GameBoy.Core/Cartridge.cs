namespace Diamond.GameBoy.Core
{
    public static class Constants
    {
        public static readonly byte[] NintendoLogo =
        {
            0xCE, 0xED, 0x66, 0x66, 0xCC, 0x0D, 0x00, 0x0B,
            0x03, 0x73, 0x00, 0x83, 0x00, 0x0C, 0x00, 0x0D,
            0x00, 0x08, 0x11, 0x1F, 0x88, 0x89, 0x00, 0x0E,
            0xDC, 0xCC, 0x6E, 0xE6, 0xDD, 0xDD, 0xD9, 0x99,
            0xBB, 0xBB, 0x67, 0x63, 0x6E, 0x0E, 0xEC, 0xCC,
            0xDD, 0xDC, 0x99, 0x9F, 0xBB, 0xB9, 0x33, 0x3E
        };
    }

    public sealed class Cartridge
    {
        // 0100 - 0103  Entry Point
        // 0104 - 0133  Nintendo Logo
        // 0143 - 0143  Title
        // 013F - 0142  Manufacturer Code
        // 0143         CGB Flag
        // 0144 - 0145  New Licensee Code
        // 0146         SGB Flag
        // 0147         Cartridge Type
        // 0148         ROM Size
        // 0149         RAM Size
        // 014A         Destination Code
        // 014B         Old License Code
        // 014C         Mask ROM Version Number
        // 014D         Header Checksum
        // 014E - 014F  Global Checksum
    }
}
