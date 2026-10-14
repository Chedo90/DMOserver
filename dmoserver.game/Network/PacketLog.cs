namespace dmoserver.game.Packets;

public static class PacketLog
{
    public static void Out(int opcode, byte[] bytes)
    {
        Console.WriteLine($"[OUT {opcode} RAW] {BitConverter.ToString(bytes).Replace("-", "")}");
    }
}