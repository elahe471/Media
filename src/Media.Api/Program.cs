


var builder = WebApplication.CreateBuilder(args);
builder.AddApplicationServices();
builder.Services.AddOpenApi();
builder.AddApplicationValidation();
builder.AddMinIO();

builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapGroup("/api/v1/Media")
   .WithTags("Media APIs")
   .MapMediaEndpoints();


app.Run();