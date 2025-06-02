FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the code
COPY . ./
RUN dotnet build -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Final image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables with default values
ENV SERVERURL=http://localhost:8000
ENV MQTTSERVER=localhost
ENV MQTTPORT=1883
ENV MQTTUSERNAME=
ENV MQTTPASSWORD=
ENV MQTTTOPIC=gsender
ENV MQTTCLIENTID=gsender2mqtt

ENTRYPOINT ["dotnet", "gsender2mqtt.dll"]
