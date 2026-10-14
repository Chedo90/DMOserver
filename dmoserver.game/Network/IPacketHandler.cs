namespace dmoserver.game.Network;

using System.Threading.Tasks;

public interface IPacketHandler
{
    Task HandleAsync(GameClient client, GamePacketReader packet);
}
