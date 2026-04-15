using System;

namespace RegionSnabApp
{
    public class Order
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string Initiator { get; set; } = "";
        public string Department { get; set; } = "";
        public string ItemName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; } = "Новая";
    }
}
