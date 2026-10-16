using System.Security.Cryptography;
using DineMaster.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Application.Services;

/// <summary>Taklif (referal) va bir martalik kodlar: chalkash belgilarsiz (0/O, 1/I yo'q).</summary>
public static class ReferralCodes
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Random(int length)
        => new(Enumerable.Range(0, length).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());

    public static async Task<string> NewUniqueAsync(IAppDbContext db, CancellationToken ct)
    {
        for (var i = 0; i < 10; i++)
        {
            var code = Random(8);
            if (!await db.Profiles.AnyAsync(p => p.ReferralCode == code, ct)) return code;
        }
        return Random(12);
    }
}
