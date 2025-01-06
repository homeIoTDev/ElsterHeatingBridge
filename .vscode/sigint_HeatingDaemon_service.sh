#!/bin/bash
ps -eaf | grep HeatingMqttService | grep -v grep | awk '{print $2}' | xargs kill -s SIGINT
