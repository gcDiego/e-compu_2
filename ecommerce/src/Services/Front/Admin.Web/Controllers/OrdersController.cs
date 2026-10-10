using Admin.Web.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Web.Controllers;
public sealed class OrdersController(AdminApiClient api):SecureController
{
    public async Task<IActionResult> Index(CancellationToken ct,int page=1)=>View(await api.Orders(page,ct));
    [HttpGet] public async Task<IActionResult> Details(int id,CancellationToken ct)=>await api.OrderDetail(id,ct) is { } order?View(order):NotFound();
    [HttpGet] public async Task<IActionResult> Report(DateTime? from,DateTime? to,string? transactionId,CancellationToken ct){var start=from??DateTime.UtcNow.Date.AddDays(-30);var end=to??DateTime.UtcNow.Date;ViewBag.From=start;ViewBag.To=end;ViewBag.Transaction=transactionId;return View(await api.Report(start,end,transactionId,ct));}
    [HttpPost] public async Task<IActionResult> Export(DateTime from,DateTime to,string? transactionId,CancellationToken ct){var rows=await api.Report(from,to,transactionId,ct);using var book=new XLWorkbook();var sheet=book.Worksheets.Add("Ventas");var headers=new[]{"Fecha","Cliente","Producto","Precio","Cantidad","Total","Transacción"};for(var i=0;i<headers.Length;i++)sheet.Cell(1,i+1).Value=headers[i];var row=2;foreach(var x in rows){sheet.Cell(row,1).Value=x.Date.DateTime;sheet.Cell(row,2).Value=x.Customer;sheet.Cell(row,3).Value=x.Product;sheet.Cell(row,4).Value=x.UnitPrice;sheet.Cell(row,5).Value=x.Quantity;sheet.Cell(row,6).Value=x.Total;sheet.Cell(row,7).Value=x.TransactionId;row++;}using var stream=new MemoryStream();book.SaveAs(stream);return File(stream.ToArray(),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",$"ventas-{DateTime.UtcNow:yyyyMMdd}.xlsx");}
}
