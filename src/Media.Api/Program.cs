

var builder = WebApplication.CreateBuilder(args);
builder.AddApplicationServices();
builder.Services.AddOpenApi();
builder.AddApplicationValidation();
builder.AddMinIO();
builder.Services.AddCors(options =>
{
    options.AddPolicy("MediaCors", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("MediaCors");
app.UseHttpsRedirection();
app.MapGroup("/api/v1/Media")
   .WithTags("Media APIs")
   .MapMediaEndpoints();


app.Run();