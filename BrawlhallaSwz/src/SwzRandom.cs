namespace BrawlhallaSwz;

public class SwzRandom
{
    private int _index;
    private readonly uint[] _state = new uint[16];

    public SwzRandom(uint seed)
    {
        _index = 0;
        _state[0] = seed;
        for (uint i = 1; i < 16; ++i) _state[i] = i + 0x6C078965u * (_state[i - 1] ^ (_state[i - 1] >> 30));
    }

    public uint Next()
    {
        uint a, b, c, d;

        a = _state[_index];
        b = _state[(_index + 13) % 16];
        c = a ^ (a << 16) ^ b ^ (b << 15);
        b = _state[(_index + 9) % 16];
        b ^= b >> 11;
        _state[_index] = b ^ c;
        a = _state[_index];
        d = a ^ ((a << 5) & 0xDA442D24u);
        _index = (_index + 15) % 16;
        a = _state[_index];
        _state[_index] = a ^ (a << 2) ^ (b << 28) ^ c ^ (c << 18) ^ d;

        return _state[_index];
    }
}
