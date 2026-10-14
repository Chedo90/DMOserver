namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;
using dmoserver.game.Packets;

public sealed class HandshakeHandler : IPacketHandler
{
    public async Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        packet.ReadByte();
        var handshakeTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var handshake = (short)(client.ClientHandshake ^ GameClient.HandshakeDegree);
        byte[] connResponse = new ConnectionPacket(handshake, handshakeTimestamp).Serialize();
        await client.SendAsync(connResponse);
    }
}
