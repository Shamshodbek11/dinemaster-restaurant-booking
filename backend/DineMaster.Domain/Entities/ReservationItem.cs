using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("reservation_items")]
public class ReservationItem
{
    [Column("reservation_id")] public Guid ReservationId { get; set; }
    [Column("menu_item_id")] public Guid MenuItemId { get; set; }
    [Column("quantity")] public int Quantity { get; set; } = 1;
    [Column("unit_price", TypeName = "numeric(12,2)")] public decimal UnitPrice { get; set; }
    public Reservation? Reservation { get; set; }
    public MenuItem? MenuItem { get; set; }
}
