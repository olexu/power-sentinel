.PHONY: restore build run watch npm-install docker-build docker-up clean

PROJECT=./power-sentinel/power-sentinel.csproj

restore:
	@if [ -d ./power-sentinel/node_modules ]; then \
		echo "node_modules present, skipping npm ci"; \
	else \
		npm ci --prefix ./power-sentinel; \
	fi
	dotnet restore $(PROJECT)

build: restore
	dotnet build $(PROJECT) -c Release

run:
	dotnet run --project $(PROJECT) --debug

watch:
	dotnet watch --project $(PROJECT) run

npm-install:
	npm ci --prefix ./power-sentinel

docker-build:
	docker compose build

docker-up:
	docker compose up -d

clean:
	dotnet clean $(PROJECT)
