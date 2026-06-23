using System;
using System.Collections.Generic;

namespace Motd.Core.Modules;

/// <summary>
/// Minimal Valve <c>bf_write</c> subset + a single-entry string-table delta encoder.
///
/// ModSharp exposes the string table (Find/GetId/FindStringIndex/SetStringUserData) and
/// <c>SendNetMessage</c>, but NOT a serializer for <c>svc_UpdateStringTable.string_data</c> —
/// that field is the engine's bit-packed delta blob, not protobuf. This builds it in managed code.
///
/// Bit semantics ported from Source 2 <c>public/tier1/bitbuf.h</c>: bits pack LSB-first so the
/// client's <c>bf_read</c> decodes them. Encoding shape (one changed entry, existing key, new
/// userdata) credited to ipsvn/ReplicateStringTableValue.
/// </summary>
internal sealed class BitWriter
{
    private readonly List<byte> _bytes = new();
    private ulong _scratch;        // pending bits, LSB-first
    private int   _bitsInScratch;

    /// <summary>Write the low <paramref name="numBits"/> bits of <paramref name="data"/>, LSB-first.</summary>
    public void WriteUBitLong(uint data, int numBits)
    {
        if (numBits < 32)
            data &= (1u << numBits) - 1;

        _scratch |= (ulong) data << _bitsInScratch;
        _bitsInScratch += numBits;

        while (_bitsInScratch >= 8)
        {
            _bytes.Add((byte) (_scratch & 0xFF));
            _scratch >>= 8;
            _bitsInScratch -= 8;
        }
    }

    public void WriteOneBit(int bit) => WriteUBitLong((uint) (bit & 1), 1);

    public void WriteBytes(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            WriteUBitLong(b, 8);
    }

    /// <summary>Protobuf varint (LEB128); each byte emitted at the current (possibly unaligned) bit offset.</summary>
    public void WriteVarInt32(uint data)
    {
        while (data >= 0x80)
        {
            WriteUBitLong((data & 0x7F) | 0x80, 8);
            data >>= 7;
        }

        WriteUBitLong(data, 8);
    }

    /// <summary>Source 2 <c>CBitWrite::WriteUBitVar</c> (bitbuf.h) — 6/10/14/(6+28)-bit scheme.</summary>
    public void WriteUBitVar(uint n)
    {
        if (n < 16)
            WriteUBitLong(n, 6);
        else if (n < 256)
            WriteUBitLong((n & 15) | 16 | ((n & 0xF0) << 2), 10);
        else if (n < 4096)
            WriteUBitLong((n & 15) | 32 | ((n & 0xFF0) << 2), 14);
        else
        {
            WriteUBitLong((n & 15) | 48, 6);
            WriteUBitLong(n >> 4, 28);
        }
    }

    /// <summary>Flush to bytes; trailing bits of the final byte are zero-padded (matches GetNumBytesWritten()).</summary>
    public byte[] ToArray()
    {
        if (_bitsInScratch > 0)
        {
            _bytes.Add((byte) (_scratch & 0xFF));
            _scratch       = 0;
            _bitsInScratch = 0;
        }

        return _bytes.ToArray();
    }
}

internal static class StringTableDelta
{
    /// <summary>
    /// Build <c>string_data</c> for one changed entry that keeps its existing key and gets new
    /// userdata. Pair with <c>CSVCMsg_UpdateStringTable{ NumChangedEntries = 1 }</c>.
    /// </summary>
    public static byte[] EncodeSingleUserData(int stringIndex, ReadOnlySpan<byte> userData)
    {
        var bw = new BitWriter();

        bw.WriteOneBit(0);                              // explicit index follows (not last+1)
        bw.WriteVarInt32((uint) (stringIndex - 1));     // engine adds 1 back on read
        bw.WriteOneBit(0);                              // no new key name (reuse existing)
        bw.WriteOneBit(1);                              // has userdata
        bw.WriteUBitVar((uint) userData.Length);
        bw.WriteBytes(userData);

        return bw.ToArray();
    }
}
