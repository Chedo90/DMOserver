using dmoserver.game.Network;

namespace dmoserver.game.Packets;

public class ExpUpdatePacket
{
    private readonly long _tamerExp;
    private readonly long _bonusTamerExp;
    private readonly long _tamerFinalExp;
    private readonly uint _partnerHandle;
    private readonly long _partnerExp;
    private readonly long _bonusPartnerExp;
    private readonly long _partnerFinalExp;
    private readonly uint _skillExp;

    public short Opcode => 1018; // OpCode oficial para recibir EXP

    public ExpUpdatePacket(long tamerExp, long bonusTamerExp, long tamerFinalExp, 
                           uint partnerHandle, long partnerExp, long bonusPartnerExp, 
                           long partnerFinalExp, uint skillExp)
    {
        _tamerExp = tamerExp;
        _bonusTamerExp = bonusTamerExp;
        _tamerFinalExp = tamerFinalExp;
        _partnerHandle = partnerHandle;
        _partnerExp = partnerExp;
        _bonusPartnerExp = bonusPartnerExp;
        _partnerFinalExp = partnerFinalExp;
        _skillExp = skillExp;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(Opcode);
        
        // El cliente de DMO requiere los valores multiplicados por 100 para la barra
        writer.WriteLong(_tamerExp * 100);
        writer.WriteLong(_bonusTamerExp * 100);
        writer.WriteLong(_tamerFinalExp * 100);
        
        writer.WriteUInt(_partnerHandle);
        
        writer.WriteLong(_partnerExp * 100);
        writer.WriteLong(_bonusPartnerExp * 100);
        writer.WriteLong(_partnerFinalExp * 100);
        
        writer.WriteUInt(_skillExp);

        return writer.Serialize();
    }
}