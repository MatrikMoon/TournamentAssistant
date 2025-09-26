using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using TournamentAssistantServer.ASP.Activators;
using TournamentAssistantServer.ASP.Filters;
using TournamentAssistantServer.ASP.Middleware;
using TournamentAssistantServer.ASP.Providers;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.Utilities;

namespace TournamentAssistantServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Transient<IControllerActivator, PropertyInjectionActivator>());

            services.AddHttpContextAccessor();

            services.AddScoped(provider =>
            {
                var httpContext = provider.GetRequiredService<IHttpContextAccessor>().HttpContext;

                // Right now, we only support grabbing the user from the token, not the currently loaded modules or corresponding packet
                return new ExecutionContext(null, httpContext.GetUserFromToken(), null);
            });

            // This is different from the other filters because we don't run it on every endpoint
            services.AddScoped<RequirePermissionFilter>();

            services.AddControllers(options =>
            {
                options.Filters.Add<ClientTypeAuthorizationFilter>();
                options.Filters.Add<PopulatePacketFieldsFilter>();
                options.ModelBinderProviders.Insert(0, new UserFromTokenBinderProvider());
            });

            services.AddSwaggerGen(options =>
            {
                options.CustomSchemaIds(type =>
                {
                    // Use the full name (including namespace and nested classes)
                    // This avoids name conflicts for OneOfCase among others
                    return type.FullName.Replace('+', '.');
                });

                options.SwaggerDoc("v1", new OpenApiInfo { Title = "TA API", Version = "v1" });
                options.OperationFilter<SwaggerIgnoreParameterFilter>();

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "TA Token, required for most requests"
                });
                
                options.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme()
                        {
                            Reference = new OpenApiReference()
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] { }
                    }
                });
            });

            services.AddCors(options =>
            {
                // options.AddPolicy("AllowUsers", policy =>
                // {
                //     policy.WithOrigins("http://localhost:1420", "https://tournamentassistant.net")
                //           .AllowAnyHeader()
                //           .AllowAnyMethod();
                // });

                // options.AddPolicy("AllowAll", builder =>
                // {
                //     builder
                //         .AllowAnyOrigin()
                //         .AllowAnyMethod()
                //         .AllowAnyHeader();
                // });

                // Allow requests from anywhere. Whoever is making a TA app should be able to fetch images from other than localhost:1420 origin
                // - Luna
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // TODO: remove
            app.UseDeveloperExceptionPage();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseMiddleware<TokenParsingMiddleware>();

            app.UseSwagger();
            app.UseSwaggerUI(c => {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "TA API v1");
            });

            // switcharooni pepperooni
            // app.UseCors("AllowUsers");
            app.UseCors("AllowAll");

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
