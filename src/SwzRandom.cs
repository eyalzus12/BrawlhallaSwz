namespace BrawlhallaSwz;

public class SwzRandom
{
    public int Index { get; private set; }
    public uint[] State { get; private set; } = new uint[16];

    public SwzRandom(uint seed)
    {
        Init(seed);
    }

    public void Init(uint seed)
    {
        Index = 0;
        State[0] = seed;
        for (uint i = 1; i < 16; ++i) State[i] = i + 0x6c078965u * (State[i - 1] ^ (State[i - 1] >> 30));
    }

    public uint Next()
    {
        uint a, b, c, d;

        a = State[Index];
        b = State[(Index + 13) & 15];
        c = a ^ (a << 16) ^ b ^ (b << 15);
        b = State[(Index + 9) & 15];
        b ^= b >> 11;
        State[Index] = c ^ b;
        a = State[Index];
        d = a ^ ((a << 5) & 0xda442d24u);
        Index = (Index + 15) & 15;
        a = State[Index];
        State[Index] = a ^ (a << 2) ^ c ^ (c << 18) ^ d ^ (b << 28);

        return State[Index];
    }
}
