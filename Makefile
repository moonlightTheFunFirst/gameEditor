SOLUTION := gameEditor.sln
PROJECT := src/GameEditor.App/GameEditor.App.csproj
CONFIG ?= Debug
RUNTIME ?= win-x64
OUTPUT := Output
DOTNET := dotnet

.PHONY: all restore build rebuild run deploy clean

all: build

restore:
	$(DOTNET) restore $(SOLUTION)

build: restore
	$(DOTNET) build $(SOLUTION) -c $(CONFIG) --no-restore

rebuild: clean build

run: build
	$(DOTNET) run --project $(PROJECT) -c $(CONFIG) --no-build

deploy:
	if exist "$(OUTPUT)" rmdir /S /Q "$(OUTPUT)"
	$(DOTNET) restore $(PROJECT) -r $(RUNTIME)
	$(DOTNET) publish $(PROJECT) -c Release -r $(RUNTIME) --self-contained false --no-restore -o "$(OUTPUT)"

clean:
	$(DOTNET) clean $(SOLUTION) -c $(CONFIG)
