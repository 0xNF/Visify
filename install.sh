#ensuring that var are properly sourced

# Ensure the Service is executable
echo "Ensuring Visify service is executable"
chmod +x ./visify.service

# Replace the systemd service file
echo "Stopping old Visify Systemd service if it exists"
systemctl stop visify

# Rebuilding Swipr Server
echo "Rebuilding Dotnet binaries"
dotnet publish -c Release ./

echo "Copying new Swipr Server Systemd service"
cp ./visify.service /etc/systemd/system/

echo "Reloading daemons"
systemctl daemon-reload

# Restart the systemd service
echo "Restarting Visify service"
systemctl start visify