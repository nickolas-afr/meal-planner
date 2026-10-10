# Source this to use the project-local .NET SDK: `source .tools/env.sh`
_root="$(cd "$(dirname "${BASH_SOURCE[0]:-${(%):-%x}}")/.." && pwd)"
export DOTNET_ROOT="$_root/.tools/dotnet"
export DOTNET_CLI_HOME="$_root/.tools/dotnet-home"
export NUGET_PACKAGES="$_root/.tools/nuget/packages"
export DOTNET_MULTILEVEL_LOOKUP=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export GH_CONFIG_DIR="$_root/.tools/gh-config"
export PATH="$DOTNET_ROOT:$_root/.tools/gh/bin:$_root/.tools/ef:$PATH"
unset _root
