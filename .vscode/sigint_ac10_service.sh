#!/bin/bash
ps -eaf | grep AC10HeatingMqttService | grep -v grep | awk '{print $2}' | xargs kill -s SIGINT
