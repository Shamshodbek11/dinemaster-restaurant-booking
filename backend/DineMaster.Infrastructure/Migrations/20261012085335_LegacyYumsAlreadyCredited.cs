using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DineMaster.Infrastructure.Migrations
{
    /// <summary>
    /// Faqat ma'lumot migratsiyasi (sxema o'zgarmaydi).
    /// Eski kod bron uchun yumsni bron paytida bergan edi; yangi qoidada yums tashrif
    /// yakunlangach ("completed") beriladi. Mavjud bronlar uchun bonus allaqachon berilgan —
    /// ikki marta tushmasligi uchun ularning yums_earned qiymati 0 qilinadi.
    /// </summary>
    public partial class LegacyYumsAlreadyCredited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE reservations SET yums_earned = 0 WHERE yums_earned <> 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Qaytarib bo'lmaydi: qaysi bronlarda qancha bo'lgani saqlanmagan (eski qiymat har doim 100 edi)
            migrationBuilder.Sql("UPDATE reservations SET yums_earned = 100 WHERE yums_earned = 0 AND status <> 'cancelled';");
        }
    }
}
