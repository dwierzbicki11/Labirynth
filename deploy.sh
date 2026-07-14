#!/bin/bash
# ==============================================================================
# CYBERENGINE: PEŁNY PROTOKÓŁ (19 WEKTORÓW) - CZYSTA ARCHITEKTURA ZDALNA (SSH)
# ==============================================================================

LAPTOP="10.0.0.3"
RPI="10.0.0.2"

PATH_X64="/home/zonderq/Labirynth/bin/Release/net11.0/linux-x64/publish/CyberEngine"
PATH_ARM="/home/zonderq/Labirynth/bin/Release/net11.0/linux-arm64/publish/CyberEngine"

LOG_DIR="test_results_19_$(date +%Y%m%d_%H%M%S)"
mkdir -p $LOG_DIR

# ------------------------------------------------------------------------------
# WSTRZYKIWANIE ZMIENNYCH ŚRODOWISKOWYCH (Rozwiązanie błędu .NET na SSH)
# ------------------------------------------------------------------------------
DOTNET_ENV="export DOTNET_ROOT=/home/zonderq/.dotnet; export PATH=\$PATH:/home/zonderq/.dotnet"

HEADLESS_ENV="export HEADLESS=1; $DOTNET_ENV"
X64_ENV="export DISPLAY=:0; $DOTNET_ENV"
# Poprawiono ścieżkę autoryzacji X11 dla użytkownika zonderq na malince
RPI_ENV="export DISPLAY=:0; export XAUTHORITY=/home/zonderq/.Xauthority; export XDG_RUNTIME_DIR=/run/user/1000; $DOTNET_ENV"

echo ">>> ROZPOCZĘCIE 19-STOPNIOWEJ PROCEDURY TESTOWEJ (100% SSH) <<<"

# --- SEKCJA I: CZYSTA WYDAJNOŚĆ OBLICZENIOWA (HEADLESS) ---
echo "[Test 1/19] Benchmark Logiki (5000 klatek)"
ssh zonderq@$RPI "$HEADLESS_ENV; $PATH_ARM --benchmark 5000" > "$LOG_DIR/test_1_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$HEADLESS_ENV; $PATH_X64 --benchmark 5000" > "$LOG_DIR/test_1_x64.log" 2>&1 &
wait

echo "[Test 2/19] Stress Test CPU (30 sekund)"
ssh zonderq@$RPI "$HEADLESS_ENV; $PATH_ARM --stress-test 30" > "$LOG_DIR/test_2_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$HEADLESS_ENV; $PATH_X64 --stress-test 30" > "$LOG_DIR/test_2_x64.log" 2>&1 &
wait

echo "[Test 3/19] Fuzzing Pamięci (50000 iteracji)"
ssh zonderq@$RPI "$HEADLESS_ENV; $PATH_ARM --fuzz-mode 50000" > "$LOG_DIR/test_3_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$HEADLESS_ENV; $PATH_X64 --fuzz-mode 50000" > "$LOG_DIR/test_3_x64.log" 2>&1 &
wait

# --- SEKCJA II: SKALOWANIE PRESETÓW GRAFICZNYCH (OBA WĘZŁY) ---
echo "[Test 4/19] Render: Preset LOW (2000 klatek)"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --preset low" > "$LOG_DIR/test_4_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --preset low" > "$LOG_DIR/test_4_x64.log" 2>&1 &
wait

echo "[Test 5/19] Render: Preset MED (2000 klatek)"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --preset med" > "$LOG_DIR/test_5_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --preset med" > "$LOG_DIR/test_5_x64.log" 2>&1 &
wait

echo "[Test 6/19] Render: Preset HIGH (2000 klatek)"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --preset high" > "$LOG_DIR/test_6_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --preset high" > "$LOG_DIR/test_6_x64.log" 2>&1 &
wait

# --- SEKCJA III: TESTY ROZDZIELCZOŚCI I RENDER SCALE (OBA WĘZŁY) ---
echo "[Test 7/19] Rozdzielczość: 800x600 LOW"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --resolution 800x600 --preset low" > "$LOG_DIR/test_7_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --resolution 800x600 --preset low" > "$LOG_DIR/test_7_x64.log" 2>&1 &
wait

echo "[Test 8/19] Rozdzielczość: 1920x1080 HIGH"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --resolution 1920x1080 --preset high" > "$LOG_DIR/test_8_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --resolution 1920x1080 --preset high" > "$LOG_DIR/test_8_x64.log" 2>&1 &
wait

echo "[Test 9/19] Render Scale: 0.5"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --render-scale 0.5" > "$LOG_DIR/test_9_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --render-scale 0.5" > "$LOG_DIR/test_9_x64.log" 2>&1 &
wait

# --- SEKCJA IV: KONTROLA SYNCHRONIZACJI (OBA WĘZŁY) ---
echo "[Test 10/19] VSync: ON"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --vsync 1" > "$LOG_DIR/test_10_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --vsync 1" > "$LOG_DIR/test_10_x64.log" 2>&1 &
wait

echo "[Test 11/19] VSync: OFF"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --vsync 0" > "$LOG_DIR/test_11_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --vsync 0" > "$LOG_DIR/test_11_x64.log" 2>&1 &
wait

echo "[Test 12/19] FPS Limit: 30 FPS"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 1000 --vsync 0 --fps-limit 30" > "$LOG_DIR/test_12_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 1000 --vsync 0 --fps-limit 30" > "$LOG_DIR/test_12_x64.log" 2>&1 &
wait

echo "[Test 13/19] FPS Limit: 60 FPS"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 1000 --vsync 0 --fps-limit 60" > "$LOG_DIR/test_13_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 1000 --vsync 0 --fps-limit 60" > "$LOG_DIR/test_13_x64.log" 2>&1 &
wait

# --- SEKCJA V: ZAAWANSOWANE FUNKCJE VULKANA (OBA WĘZŁY) ---
echo "[Test 14/19] Cienie: OFF"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --shadows 0" > "$LOG_DIR/test_14_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --shadows 0" > "$LOG_DIR/test_14_x64.log" 2>&1 &
wait

echo "[Test 15/19] Cienie: ON"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --shadows 1" > "$LOG_DIR/test_15_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --shadows 1" > "$LOG_DIR/test_15_x64.log" 2>&1 &
wait

echo "[Test 16/19] Post-Processing: Bloom i AO OFF"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --bloom 0 --ao 0" > "$LOG_DIR/test_16_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --bloom 0 --ao 0" > "$LOG_DIR/test_16_x64.log" 2>&1 &
wait

echo "[Test 17/19] Post-Processing: Bloom i AO ON"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --benchmark 2000 --bloom 1 --ao 1" > "$LOG_DIR/test_17_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --benchmark 2000 --bloom 1 --ao 1" > "$LOG_DIR/test_17_x64.log" 2>&1 &
wait

# --- SEKCJA VI: SYMULACJA POLA WALKI (TESTY ASYMETRYCZNE SYMULTANICZNE) ---
echo "[Test 18/19] Test Zderzeniowy Alfa"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --stress-test 45" > "$LOG_DIR/test_18_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --stress-test 45" > "$LOG_DIR/test_18_x64.log" 2>&1 &
wait

echo "[Test 19/19] Test Zderzeniowy Omega"
ssh zonderq@$RPI "$RPI_ENV; $PATH_ARM --fuzz-mode 150000" > "$LOG_DIR/test_19_rpi.log" 2>&1 &
ssh zonderq@$LAPTOP "$X64_ENV; $PATH_X64 --fuzz-mode 150000" > "$LOG_DIR/test_19_x64.log" 2>&1 &
wait

echo "=============================================================================="
echo " PEŁNY PROTOKÓŁ ZAKOŃCZONY. RAPORTY DOSTĘPNE W LOKALNYM FOLDERZE: $LOG_DIR"
echo "=============================================================================="