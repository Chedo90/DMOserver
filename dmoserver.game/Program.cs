using dmoserver.game.Network;

const int port = 7031;

var server = new GameServer(port);
await server.StartAsync();