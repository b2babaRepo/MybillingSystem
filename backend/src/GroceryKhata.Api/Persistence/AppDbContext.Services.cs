using System.Reflection;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Planner;
using GroceryKhata.Api.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryKhata.Api.Persistence;

public sealed partial class AppDbContext
{
    /// <summary>Service trades: dairy collection, rooms and stays, venues and tables, salons, prescriptions, jobs and AMCs.</summary>
    private void ConfigureServices(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<MeterReading>(b =>
        {
            b.ToTable("meter_readings");
            b.HasTenantKey();
            b.Property(r => r.Note).HasMaxLength(200);
            b.HasIndex(r => new { r.ShopId, r.Day });
        });

        modelBuilder.Entity<Amc>(b =>
        {
            b.ToTable("amcs");
            b.HasTenantKey();
            b.Property(x => x.Title).HasMaxLength(120);
            b.Property(x => x.SerialNo).HasMaxLength(40);
            b.Property(x => x.Fee).HasPrecision(12, 2);
            b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<Trip>(b =>
        {
            b.ToTable("trips");
            b.HasTenantKey();
            b.Property(x => x.Technician).HasMaxLength(60);
            b.Property(x => x.Place).HasMaxLength(80);
            b.Property(x => x.Returned).HasPrecision(12, 2);
            b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<TripPhoto>(b =>
        {
            b.ToTable("trip_photos");
            b.HasKey(x => x.EntryId);
            b.HasIndex(x => x.ShopId);
            b.Property(x => x.ContentType).HasMaxLength(20);
        });

        modelBuilder.Entity<TripEntry>(b =>
        {
            b.ToTable("trip_entries");
            b.HasTenantKey();
            b.Property(x => x.Description).HasMaxLength(80);
            b.Property(x => x.Amount).HasPrecision(12, 2);
            b.HasIndex(x => new { x.ShopId, x.TripId });
        });

        modelBuilder.Entity<WarrantyClaim>(b =>
        {
            b.ToTable("warranty_claims");
            b.HasTenantKey();
            b.Property(c => c.Parts).HasPrecision(12, 2);
            b.Property(c => c.Labour).HasPrecision(12, 2);
            b.Property(c => c.CreditAmount).HasPrecision(12, 2);
            b.Property(c => c.Description).HasMaxLength(200);
            b.Property(c => c.RejectReason).HasMaxLength(200);
            b.Property(c => c.SerialNo).HasMaxLength(40);
            b.Property(c => c.DispatchNo).HasMaxLength(40);
            b.Property(c => c.CreditNoteNo).HasMaxLength(40);
            b.HasIndex(c => new { c.ShopId, c.Number }).IsUnique();
        });

        modelBuilder.Entity<Job>(b =>
        {
            b.ToTable("jobs");
            b.Property(x => x.SerialNo).HasMaxLength(40);
            b.Property(x => x.Complaint).HasMaxLength(300);
            b.HasIndex(x => new { x.ShopId, x.SerialNo });
            b.HasTenantKey();
            b.Property(j => j.Title).HasMaxLength(120);
            b.Property(j => j.Note).HasMaxLength(300);
            b.Property(j => j.Price).HasPrecision(12, 2);
            b.Property(j => j.EstimatedCost).HasPrecision(12, 2);
            b.HasIndex(j => new { j.ShopId, j.Number }).IsUnique();
        });

        modelBuilder.Entity<Venue>(b =>
        {
            b.ToTable("venues");
            b.HasTenantKey();
            b.Property(v => v.Name).HasMaxLength(60);
        });

        modelBuilder.Entity<Booking>(b =>
        {
            b.ToTable("bookings");
            b.HasTenantKey();
            b.Property(x => x.PlateRate).HasPrecision(12, 2);
            b.Property(x => x.HallRent).HasPrecision(12, 2);
            b.Property(x => x.Deposit).HasPrecision(12, 2);
            b.Property(x => x.DepositTaken).HasPrecision(12, 2);
            b.Property(x => x.DepositReturned).HasPrecision(12, 2);
            b.Property(x => x.Note).HasMaxLength(300);
            b.Property(x => x.CancelReason).HasMaxLength(200);
            b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.ShopId, x.VenueId, x.Date });
        });

        modelBuilder.Entity<TableBooking>(b =>
        {
            b.ToTable("table_bookings");
            b.HasTenantKey();
            b.Property(x => x.Name).HasMaxLength(80);
            b.Property(x => x.Phone).HasMaxLength(20);
            b.Property(x => x.Cake).HasMaxLength(120);
            b.Property(x => x.Table).HasMaxLength(30);
            b.Property(x => x.Note).HasMaxLength(200);
            b.Property(x => x.Deposit).HasPrecision(12, 2);
            b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.ShopId, x.Date });
        });

        modelBuilder.Entity<Room>(b =>
        {
            b.ToTable("rooms");
            b.HasTenantKey();
            b.Property(r => r.Name).HasMaxLength(30);
            b.Property(r => r.Kind).HasMaxLength(40);
            b.Property(r => r.Rate).HasPrecision(12, 2);
            b.Property(r => r.IcalImportUrl).HasMaxLength(500);
            b.Property(r => r.IcalToken).HasMaxLength(40);
            b.HasIndex(r => r.IcalToken).IsUnique();
        });

        modelBuilder.Entity<RatePeriod>(b =>
        {
            b.ToTable("rate_periods");
            b.HasTenantKey();
            b.Property(p => p.Name).HasMaxLength(60);
            b.Property(p => p.Rate).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Stay>(b =>
        {
            b.ToTable("stays");
            b.HasTenantKey();
            b.Property(s => s.GuestName).HasMaxLength(80);
            b.Property(s => s.Phone).HasMaxLength(20);
            b.Property(s => s.IdType).HasMaxLength(30);
            b.Property(s => s.IdLast4).HasMaxLength(4);
            b.Property(s => s.SourceRef).HasMaxLength(40);
            b.Property(s => s.Note).HasMaxLength(200);
            b.Property(s => s.Rate).HasPrecision(12, 2);
            b.Property(s => s.Commission).HasPrecision(12, 2);
            b.Property(s => s.RoomCharge).HasPrecision(12, 2);
            b.HasIndex(s => s.BookingId);
            b.HasMany(s => s.Charges).WithOne().HasForeignKey(c => c.StayId);
            b.HasIndex(s => new { s.ShopId, s.Number }).IsUnique();
            b.HasIndex(s => new { s.ShopId, s.RoomId, s.CheckIn });
        });

        modelBuilder.Entity<StayCharge>(b =>
        {
            b.ToTable("stay_charges");
            b.HasTenantKey();
            b.Property(c => c.Name).HasMaxLength(60);
            b.Property(c => c.Amount).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Appointment>(b =>
        {
            b.ToTable("appointments");
            b.HasTenantKey();
            b.Property(a => a.Name).HasMaxLength(80);
            b.Property(a => a.Phone).HasMaxLength(20);
            b.Property(a => a.Note).HasMaxLength(200);
            b.HasMany(a => a.Services).WithOne().HasForeignKey(s => s.AppointmentId);
            b.HasIndex(a => new { a.ShopId, a.Number }).IsUnique();
            b.HasIndex(a => new { a.ShopId, a.Date, a.StylistId });
        });

        modelBuilder.Entity<BridalBooking>(b =>
        {
            b.ToTable("bridal_bookings");
            b.HasTenantKey();
            b.Property(x => x.Title).HasMaxLength(120);
            b.Property(x => x.Address).HasMaxLength(200);
            b.Property(x => x.Note).HasMaxLength(300);
            b.Property(x => x.TravelCharge).HasPrecision(12, 2);
            b.HasMany(x => x.Events).WithOne().HasForeignKey(e => e.BookingId);
            b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<ServiceProduct>(b =>
        {
            b.ToTable("service_products");
            b.HasTenantKey();
            b.Property(s => s.Quantity).HasPrecision(12, 3);
            b.HasIndex(s => new { s.ShopId, s.ServiceId, s.ProductId }).IsUnique();
        });

        modelBuilder.Entity<DairyFarmer>(b =>
        {
            b.ToTable("dairy_farmers");
            b.HasTenantKey();
            b.Property(f => f.Name).HasMaxLength(80);
            b.Property(f => f.Phone).HasMaxLength(20);
            b.Property(f => f.Village).HasMaxLength(60);
            b.HasIndex(f => new { f.ShopId, f.Code }).IsUnique();
        });

        modelBuilder.Entity<DairyRatePlan>(b =>
        {
            b.ToTable("dairy_rate_plans");
            b.HasTenantKey();
            b.HasMany(p => p.Rates).WithOne().HasForeignKey(r => r.PlanId);
            b.HasMany(p => p.Grid).WithOne().HasForeignKey(c => c.PlanId);
            b.HasIndex(p => new { p.ShopId, p.For, p.From });
        });

        modelBuilder.Entity<DairyRate>(b =>
        {
            b.ToTable("dairy_rates");
            b.HasTenantKey();
            foreach (var p in new[] { nameof(DairyRate.FatRate), nameof(DairyRate.SnfRate), nameof(DairyRate.FlatRate) }) b.Property(p).HasPrecision(10, 2);
            b.Property(r => r.MinFat).HasPrecision(5, 2);
            b.Property(r => r.MinSnf).HasPrecision(5, 2);
        });

        modelBuilder.Entity<DairyGridCell>(b =>
        {
            b.ToTable("dairy_grid_cells");
            b.HasTenantKey();
            b.Property(c => c.Fat).HasPrecision(4, 1);
            b.Property(c => c.Snf).HasPrecision(4, 1);
            b.Property(c => c.RatePerLitre).HasPrecision(8, 2);
            b.HasIndex(c => new { c.ShopId, c.PlanId, c.Kind, c.Fat, c.Snf }).IsUnique();
        });

        modelBuilder.Entity<MilkCollection>(b =>
        {
            b.ToTable("milk_collections");
            b.HasTenantKey();
            b.Property(c => c.Litres).HasPrecision(8, 2);
            b.Property(c => c.Fat).HasPrecision(5, 2);
            b.Property(c => c.Snf).HasPrecision(5, 2);
            b.Property(c => c.Clr).HasPrecision(5, 1);
            b.Property(c => c.Rate).HasPrecision(10, 2);
            b.Property(c => c.Amount).HasPrecision(12, 2);
            b.Property(c => c.EditLog).HasMaxLength(2000);
            b.HasIndex(c => new { c.ShopId, c.Date, c.Shift });
            b.HasIndex(c => new { c.ShopId, c.FarmerId, c.Date });
        });

        modelBuilder.Entity<DairyFarmerEntry>(b =>
        {
            b.ToTable("dairy_farmer_entries");
            b.HasTenantKey();
            b.Property(e => e.Amount).HasPrecision(12, 2);
            b.Property(e => e.Note).HasMaxLength(120);
            b.HasIndex(e => new { e.ShopId, e.FarmerId, e.Date });
        });

        modelBuilder.Entity<MilkDispatch>(b =>
        {
            b.ToTable("milk_dispatches");
            b.HasTenantKey();
            b.Property(d => d.Agency).HasMaxLength(60);
            foreach (var p in new[] { nameof(MilkDispatch.Litres), nameof(MilkDispatch.AcceptedLitres) }) b.Property(p).HasPrecision(10, 2);
            foreach (var p in new[] { nameof(MilkDispatch.Fat), nameof(MilkDispatch.Snf), nameof(MilkDispatch.AcceptedFat), nameof(MilkDispatch.AcceptedSnf) }) b.Property(p).HasPrecision(5, 2);
            b.Property(d => d.Amount).HasPrecision(12, 2);
            b.HasIndex(d => new { d.ShopId, d.Date });
        });

        modelBuilder.Entity<PrescriptionPhoto>(b =>
        {
            b.ToTable("prescription_photos");
            b.HasKey(p => p.BillId);
            b.Property(p => p.ContentType).HasMaxLength(20);
            b.HasIndex(p => p.ShopId);
        });

        modelBuilder.Entity<BillPrescription>(b =>
        {
            b.ToTable("bill_prescriptions");
            b.HasTenantKey();
            b.Property(x => x.DoctorName).HasMaxLength(80);
            b.Property(x => x.DoctorAddress).HasMaxLength(200);
            b.Property(x => x.DoctorRegNo).HasMaxLength(40);
            b.Property(x => x.PatientName).HasMaxLength(80);
            b.Property(x => x.PatientAddress).HasMaxLength(200);
            b.HasIndex(x => new { x.ShopId, x.BillId }).IsUnique();
        });

        modelBuilder.Entity<ClientCard>(b =>
        {
            b.ToTable("client_cards");
            b.HasTenantKey();
            b.Property(c => c.HairType).HasMaxLength(60);
            b.Property(c => c.SkinType).HasMaxLength(60);
            b.Property(c => c.Allergies).HasMaxLength(500);
            b.Property(c => c.ColourFormula).HasMaxLength(500);
            b.Property(c => c.Notes).HasMaxLength(500);
            b.HasIndex(c => new { c.ShopId, c.CustomerId }).IsUnique();
        });

        modelBuilder.Entity<BridalEvent>(b =>
        {
            b.ToTable("bridal_events");
            b.HasTenantKey();
            b.Property(e => e.Name).HasMaxLength(40);
            b.Property(e => e.Price).HasPrecision(12, 2);
            b.HasIndex(e => new { e.ShopId, e.Date, e.StylistId });
        });

        modelBuilder.Entity<AppointmentService>(b =>
        {
            b.ToTable("appointment_services");
            b.HasTenantKey();
            b.Property(s => s.Name).HasMaxLength(120);
            b.Property(s => s.Price).HasPrecision(12, 2);
        });

        modelBuilder.Entity<StylistRate>(b =>
        {
            b.ToTable("stylist_rates");
            b.HasTenantKey();
            b.Property(r => r.Percent).HasPrecision(5, 2);
            b.HasIndex(r => new { r.ShopId, r.MembershipId }).IsUnique();
        });

        modelBuilder.Entity<StylistEarning>(b =>
        {
            b.ToTable("stylist_earnings");
            b.HasTenantKey();
            b.Property(e => e.Base).HasPrecision(12, 2);
            b.Property(e => e.Percent).HasPrecision(5, 2);
            b.Property(e => e.Amount).HasPrecision(12, 2);
            b.HasIndex(e => new { e.ShopId, e.MembershipId, e.PaidAt });
        });

        modelBuilder.Entity<PrepaidPlan>(b =>
        {
            b.ToTable("prepaid_plans");
            b.HasTenantKey();
            b.Property(p => p.Name).HasMaxLength(60);
            b.Property(p => p.Price).HasPrecision(12, 2);
            b.Property(p => p.DiscountPercent).HasPrecision(5, 2);
        });

        modelBuilder.Entity<CustomerPlan>(b =>
        {
            b.ToTable("customer_plans");
            b.HasTenantKey();
            b.Property(p => p.Name).HasMaxLength(60);
            b.Property(p => p.Price).HasPrecision(12, 2);
            b.Property(p => p.SessionValue).HasPrecision(12, 2);
            b.Property(p => p.DiscountPercent).HasPrecision(5, 2);
            b.HasIndex(p => new { p.ShopId, p.CustomerId });
            b.HasIndex(p => new { p.ShopId, p.ExpiresOn });
        });

        modelBuilder.Entity<Machine>(b =>
        {
            b.ToTable("machines");
            b.HasTenantKey();
            b.Property(m => m.Name).HasMaxLength(80);
            b.Property(m => m.Cost).HasPrecision(12, 2);
            b.Property(m => m.RatePercent).HasPrecision(5, 2);
        });

        modelBuilder.Entity<KarigarChallan>(b =>
        {
            b.ToTable("karigar_challans");
            b.HasTenantKey();
            b.Property(c => c.Karigar).HasMaxLength(80);
            b.Property(c => c.KarigarGstin).HasMaxLength(15);
            b.Property(c => c.Note).HasMaxLength(200);
            b.Property(c => c.Quantity).HasPrecision(12, 3);
            b.Property(c => c.ReceivedQuantity).HasPrecision(12, 3);
            b.Property(c => c.UnitCost).HasPrecision(12, 2);
            b.HasIndex(c => new { c.ShopId, c.Number }).IsUnique();
        });

        modelBuilder.Entity<JobEntry>(b =>
        {
            b.ToTable("job_entries");
            b.HasTenantKey();
            b.Property(e => e.Description).HasMaxLength(120);
            b.Property(e => e.Amount).HasPrecision(12, 2);
            b.Property(e => e.Quantity).HasPrecision(12, 3);
            b.HasIndex(e => new { e.ShopId, e.JobId });
        });
    }
}
