#!/usr/bin/env bash
source /home/science/visify/envvars.sh

# Configurations
RELEASE="Release"
DEBUG="Debug"
Configuration=$RELEASE

# nc versions
NETCORE21="netcoreapp2.1"
Version=$NETCORE21

sdir=/home/science/visify/Visify/bin/$Configuration/$Version/publish
cd $sdir
dotnet ./Visify.dll --server.urls "http://*:7000"