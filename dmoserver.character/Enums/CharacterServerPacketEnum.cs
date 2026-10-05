namespace dmoserver.character.Enums;

public enum CharacterServerPacketEnum : short
{
    Unknown = -99,
    KeepConnection = -3,
    Connection = -1,
    CheckNameDuplicity = 1302,
    CreateCharacter = 1303,
    DeleteCharacter = 1304,
    GetCharacterPosition = 1305,
    RequestCharacters = 1706,
    ConnectGameServer = 1703
}