# Mosquitto demo configuration

## Local broker without Internet

From the repository root:

```powershell
mosquitto -c Extra/mosquitto/mosquitto.local.conf -v
```

Use `localhost`, port `1883`, TLS off on both Dashboard and Simulator. The listener is bound to `127.0.0.1`, so other computers cannot connect to this demo broker.

## TLS and authentication

The applications support username/password and normal TLS certificate validation. For a secure broker, create the password file outside source control:

```powershell
mosquitto_passwd -c Extra/mosquitto/passwords.txt demo-user
```

Provide a CA-signed or locally trusted server certificate to Mosquitto, enable a TLS listener (commonly port `8883`), set `allow_anonymous false`, and point `password_file` at the generated file. Private keys, password files and certificates under this folder are ignored by Git.

Dashboard users enter the credentials in the connection panel. Simulator users should keep the password in an environment variable:

```powershell
$env:UDM21_MQTT_PASSWORD = "demo-password"
dotnet run --project Code/Simulators/Simulators.csproj -- --host mqtt.example.com --port 8883 --tls --username demo-user --password-env UDM21_MQTT_PASSWORD
```

Do not disable certificate validation to make a broken TLS setup connect. Import the local CA into the operating system trust store instead.
