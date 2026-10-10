using Admin.Web.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

var builder=WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json",true,true).AddEnvironmentVariables();
builder.Services.AddControllersWithViews(o=>o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.Configure<FormOptions>(o=>{o.ValueCountLimit=128;o.ValueLengthLimit=4096;o.MultipartBodyLengthLimit=5_242_880;});
builder.Services.AddDistributedMemoryCache();builder.Services.AddSession(o=>{o.Cookie.Name=builder.Environment.IsDevelopment()?".Ecommerce.Admin.Session":"__Host-Ecommerce.Admin.Session";o.Cookie.HttpOnly=true;o.Cookie.IsEssential=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;o.IdleTimeout=TimeSpan.FromMinutes(30);});
builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<AdminApiClient>();
foreach(var service in new[]{"Administration","Catalog","Customer","Order"})builder.Services.AddHttpClient(service,c=>{c.BaseAddress=new Uri(builder.Configuration[$"Services:{service}"]!);c.Timeout=TimeSpan.FromSeconds(15);});
var app=builder.Build();if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/Home/Error");app.UseHsts();}app.UseHttpsRedirection();app.Use(async(ctx,next)=>{ctx.Response.OnStarting(()=>{var h=ctx.Response.Headers;h.XContentTypeOptions="nosniff";h.XFrameOptions="DENY";h.CacheControl="no-store";h["Referrer-Policy"]="no-referrer";h["Permissions-Policy"]="camera=(), microphone=(), geolocation=(), payment=()";h.ContentSecurityPolicy="default-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' https://res.cloudinary.com data:; style-src 'self' 'unsafe-inline'; script-src 'self'";return Task.CompletedTask;});await next();});app.UseStaticFiles();app.UseRouting();app.UseSession();app.UseAuthorization();app.MapControllerRoute("default","{controller=Dashboard}/{action=Index}/{id?}");app.Run();
public partial class Program { }
