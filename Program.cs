using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MininalAPI.Dominio.DTOs;
using MininalAPI.Dominio.Entidades;
using MininalAPI.Dominio.Interfaces;
using MininalAPI.Infraestrutura.Db;
using MininalAPI.Dominio.Servicos;
using MininalAPI.Dominio.ModelView;
using MininalAPI.Dominio.Enuns;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;


#region builder
var builder = WebApplication.CreateBuilder(args);

var Key = builder.Configuration.GetSection("Jwt").ToString();
if (string.IsNullOrEmpty(Key)) Key = "123456";
builder.Services.AddAuthentication(option =>
{
    option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;


}).AddJwtBearer(option =>
{
    option.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
        ValidateIssuer = false,
        ValidateAudience = false ,
    
    };
 });

#region  builder
builder.Services.AddAuthorization();
builder.Services.AddScoped<IAdministradorServico, AdministradorServico>();
builder.Services.AddScoped<IVeiculosServico, VeiculosServico>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = " Authotization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT aqui"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {


         new OpenApiSecurityScheme
         {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
         },
            new string[]{}
        }
    });
});

builder.Services.AddDbContext<DbContexto>(Options =>
{

    Options.UseMySql(builder.Configuration.GetConnectionString("mysql"),
ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("mysql")));
}
);
var app = builder.Build();
#endregion

#region  Home
app.MapGet("/", () => Results.Json(new Home())).AllowAnonymous().WithTags("Home");
#endregion

#region Veiculos
 ErrosDeValidacao validaDTO(VeiculoDTO veiculoDTO)
{

     var validacao = new ErrosDeValidacao
     {
        Mensagem = new List<string>()
     };
    if (string.IsNullOrEmpty(veiculoDTO.Nome))
    {
        validacao.Mensagem.Add("O nome não pode ser vazio");
    }

    if (string.IsNullOrEmpty(veiculoDTO.Marca))
    {
    validacao.Mensagem.Add("A Marca não pode ficar em branco");
    }

    if (veiculoDTO.Ano<1950)
    {
    validacao.Mensagem.Add("Veículo muito antigo,  aceita somente anos superiores a 1950");
    }

    return validacao;
}

app.MapGet("/Veiculos/", ([FromQuery] int pagina, IVeiculosServico veiculosServico) =>
{

    var veiculos = veiculosServico.Todos(pagina);


        return Results.Ok(veiculos);

        

} ).RequireAuthorization().WithTags("Veiculos");


app.MapGet("/Veiculos/{id}", ([FromRoute] int id, IVeiculosServico veiculosServico) =>
{

    var veiculo = veiculosServico.BuscadorId(id);



    if (veiculo == null) return Results.NotFound();

    return Results.Ok(veiculo);



}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" }).
WithTags("Veiculos");

app.MapPut("/Veiculos/{id}", ([FromRoute] int id, VeiculoDTO veiculoDTO, IVeiculosServico veiculosServico) =>
{

    var veiculo = veiculosServico.BuscadorId(id);
    if (veiculo == null) return Results.NotFound();

    var validacao = validaDTO(veiculoDTO);
    if (validacao.Mensagem.Count > 0)
    {
        return Results.BadRequest(validacao);
    }

    veiculo.Nome = veiculoDTO.Nome;
        veiculo.Marca = veiculoDTO.Marca;
        veiculo.Ano = veiculoDTO.Ano;

        veiculosServico.Atualizar(veiculo);

        return Results.Ok(veiculo);
} ).RequireAuthorization().WithTags("Veiculos");



app.MapDelete("/Veiculos/{id}", ([FromRoute] int id, IVeiculosServico veiculosServico) =>
{

    var veiculo = veiculosServico.BuscadorId(id);
    if (veiculo == null) return Results.NotFound();

    veiculosServico.Apagar(veiculo);

    return Results.NoContent();
}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" }).
WithTags("Veiculos");




app.MapPost("/Veiculos", ([FromBody] VeiculoDTO veiculoDTO, IVeiculosServico veiculosServico) =>
{


    var validacao = validaDTO(veiculoDTO);
    if (validacao.Mensagem.Count > 0)
    {
        return Results.BadRequest(validacao);
    }


    var veiculo = new Veiculo
    {
        Nome = veiculoDTO.Nome,
        Marca = veiculoDTO.Marca,
        Ano = veiculoDTO.Ano,
    };
    veiculosServico.Incluir(veiculo);

    return Results.Created($"/veiculo/{veiculo.Id}", veiculo);




}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute { Roles = "Adm,Editor" }).
WithTags("Veiculos");

#endregion

#region  Administradores
string GerarTokenJwt(Administrador administrador)
{
    if (string.IsNullOrEmpty(Key)) return string.Empty;
    {
        var securituKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        var credentials = new SigningCredentials(securituKey, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>()
        {
            new Claim("Email", administrador.Email),
            new Claim("Perfil", administrador.Perfil),
            new Claim(ClaimTypes.Role, administrador.Perfil),
        };
        var Token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: credentials

        );
        return new JwtSecurityTokenHandler().WriteToken(Token);
    }
    
} 
app.MapPost("/administradores/login", ([FromBody] LoginDTO loginDTO, IAdministradorServico administradorServico) =>
{
    var adm = administradorServico.Login(loginDTO);

    if (adm != null)
    {
        string token = GerarTokenJwt(adm);

        return Results.Ok(new AdministradorLogado
        {
            Email = adm.Email,
            Perfil = adm.Perfil,
            Token = token
        });
    }
    else
    {
        return Results.Unauthorized();
    }
        
}).AllowAnonymous().WithTags("Administradores");


app.MapGet("/Administradores/{Id}", ([FromRoute] int Id, IAdministradorServico administradorServico) =>
{
    var administrador = administradorServico.BuscaPorId(Id);
    if (administrador == null) return Results.NotFound();
    return Results.Ok(new AdiministradorModelView
    {
        Id = administrador.Id,

        Email = administrador.Email,
        Perfil = administrador.Perfil
    });


}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" }).
WithTags("Administradores");

app.MapGet("/administradores/", ([FromQuery] int? pagina, IAdministradorServico administradorServico) =>
{

    var adms = new List<AdiministradorModelView>();
    var administrador = administradorServico.Todos(pagina);
    foreach (var adm in administrador)
    {
        adms.Add(new AdiministradorModelView
        {
            Id = adm.Id,
            Email = adm.Email,
            Perfil = adm.Perfil


        });
    }
    return Results.Ok(adms);
}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" }).
WithTags("Administradores");



app.MapPost("/administradores", ([FromBody] AdministradorDTO administradorDTO, IAdministradorServico administradorServico) =>
{
    var validacao = new ErrosDeValidacao
    {
        Mensagem = new List<string>()
    };

    if (string.IsNullOrEmpty(administradorDTO.Email))
    {
        validacao.Mensagem.Add("Email não pode ser vazio");
    }
    if (string.IsNullOrEmpty(administradorDTO.Senha))
    {
        validacao.Mensagem.Add("Senha não pode ser vazia");
    }
    if (administradorDTO.Perfil == null)
    {
        validacao.Mensagem.Add("Perfil não pode ser vazio");
    }

    if (validacao.Mensagem.Count > 0)
    {
        return Results.BadRequest(validacao);
    }

    var administrador = new Administrador
    {
        Email = administradorDTO.Email,
        Senha = administradorDTO.Senha,
        Perfil = administradorDTO.Perfil.ToString() ?? Perfil.Editor.ToString()
    };


    administradorServico.Incluir(administrador);

    return Results.Created($"/administrador/{administrador.Id}", new AdiministradorModelView
    {
        Id = administrador.Id,
        Email = administrador.Email,
        Perfil = administrador.Perfil
    });

}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" }).
WithTags("Administradores");

#endregion



app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.Run();
#endregion