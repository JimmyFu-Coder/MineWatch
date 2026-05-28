using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MineWatch.Api.Middleware;
using MineWatch.Api.Services;
using MineWatch.Infrastructure.Data;
using Amazon.SQS;                                                                  
using MineWatch.Api.Configuration; 


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContextFactory<MineWatchDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>                                          
  {                                                                                  
      options.SwaggerDoc("v1", new() { Title = "MineWatch API", Version = "v1" });
                                                                                     
      options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme              
      {                                                                              
          Name = "Authorization",                                                    
          Description = "JWT Authorization header using the Bearer scheme.",         
          In = ParameterLocation.Header,                                             
          Type = SecuritySchemeType.Http,                                            
          Scheme = "bearer",                                                         
          BearerFormat = "JWT"                                                       
      });                                                                            
                                                                                     
      options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
      {
          {
              new OpenApiSecuritySchemeReference("Bearer", doc),                         
              new List<string>()                                                    
          }                                                                              
      });                                                                             
  });                      

builder.Services.AddControllers();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());      
builder.Services.AddAWSService<IAmazonSQS>();                                      
builder.Services.AddSingleton<SqsConfig>();                                        
builder.Services.AddHostedService<SqsBootstrapService>();
builder.Services.AddHostedService<MqttSubscriberService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(Options =>
    Options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    });


var app = builder.Build();
if (!app.Environment.IsDevelopment())
{                                    
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MineWatchDbContext>();
    await dbContext.Database.MigrateAsync();                                                                    
    await DbSeeder.SeedAsync(dbContext);
} 
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<ExceptionHandlingMiddleware>();  
app.UseAuthentication();                                                           
app.UseAuthorization();

app.MapControllers();

app.Run();