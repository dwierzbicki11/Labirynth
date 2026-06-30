#!/bin/bash
set -e 

# Konfiguracja
LOCAL_PATH="/home/zonderq/Labirynth"
REMOTE_PATH="/home/zonderq/Labirynth"
RPi_USER="zonderq"
RPi_HOST="rpi-dev"
DOTNET_PATH="/home/zonderq/.dotnet/dotnet"

# Komenda XVFB z wymuszeniem renderowania programowego
# Używamy zmiennej wewnątrz ssh, aby mieć pewność, że RPi je zaaplikuje
XVFB_CMD="export LIBGL_ALWAYS_SOFTWARE=1 && xvfb-run -s '-screen 0 800x600x24 +extension GLX +extension RANDR +extension RENDER -noreset -ac'"

# Zabezpieczenie przed błędną ścieżką
if [ -z "$LOCAL_PATH" ]; then
    echo "!!! BŁĄD: Zmienna LOCAL_PATH jest pusta! Przerywam."
    exit 1
fi

echo "========================================================"
echo "--- FABRYKA URUCHOMIONA: Rozpoczynam proces builda ---"
echo "========================================================"

echo ">>> [1/13] Lokalna naprawa formatowania kodu..."
dotnet format $LOCAL_PATH/CyberEngine.csproj --verify-no-changes || dotnet format $LOCAL_PATH/CyberEngine.csproj

echo ">>> [2/13] Synchronizacja plików na RPi..."
rsync -avz --delete --exclude 'bin' --exclude 'obj' --exclude '.git' "$LOCAL_PATH/" "$RPi_USER@$RPi_HOST:$REMOTE_PATH/"

echo ">>> [3/13] Zdalna kompilacja shaderów (glslangValidator)..."
ssh $RPi_USER@$RPi_HOST "cd $REMOTE_PATH/Shaders && for f in *.frag *.vert; do glslangValidator -V \$f -o \${f%.*}.spv; done"

echo ">>> [4/13] Shadow Build (Kompilacja Release na RPi)..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH build $REMOTE_PATH/CyberEngine.csproj -c Release"

echo ">>> [5/13] Weryfikacja stylistyki (dotnet format --verify)..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH format $REMOTE_PATH/CyberEngine.csproj --verify-no-changes"

echo ">>> [6/13] Głęboka analiza statyczna (Roslyn Analyzers)..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH build $REMOTE_PATH/CyberEngine.csproj -c Release /p:RunAnalyzersDuringBuild=true /p:EnforceCodeStyleInBuild=true"

echo ">>> [7/13] Testy jednostkowe (Blame Mode)..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH test $REMOTE_PATH/CyberEngine.csproj --configuration Release --blame-hang"

echo ">>> [8/13] Test stabilności silnika (Headless Test)..."
ssh $RPi_USER@$RPi_HOST "$XVFB_CMD timeout 10s $DOTNET_PATH exec $REMOTE_PATH/bin/Release/net11.0/CyberEngine.dll --test-mode || [ \$? -eq 124 ]"

echo ">>> [9/13] Weryfikacja odporności logiki (Fuzzing)..."
ssh $RPi_USER@$RPi_HOST "$XVFB_CMD $DOTNET_PATH exec $REMOTE_PATH/bin/Release/net11.0/CyberEngine.dll --fuzz-mode"

echo ">>> [10/13] Kompilacja AOT (Publish ReadyToRun)..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH publish $REMOTE_PATH/CyberEngine.csproj -c Release -r linux-arm64 --self-contained true -p:PublishReadyToRun=true"

echo ">>> [11/13] Audyt bezpieczeństwa bibliotek (NuGet)..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH list $REMOTE_PATH/CyberEngine.csproj package --vulnerable --format json"

echo ">>> [12/13] Analiza zależności (Dotnet List)..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH list $REMOTE_PATH/CyberEngine.csproj package"

echo ">>> [13/13] Finalny build na laptopie..."
dotnet build $LOCAL_PATH/CyberEngine.csproj -c Release

echo "========================================================"
echo "--- SUKCES: Kod przetestowany i skompilowany ---"
echo "========================================================"