.PHONY: restore build run docker-build docker-up clean

PROJECT=./power-sentinel/power-sentinel.csproj

restore:
	dotnet restore $(PROJECT)

build: restore
	dotnet build $(PROJECT) -c Release

run:
	dotnet run --project $(PROJECT) --debug

docker-build:
	docker compose build

docker-up:
	docker compose up -d

clean:
	dotnet clean $(PROJECT)
