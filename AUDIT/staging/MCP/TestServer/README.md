# AIGRAV MCP Test Server

This is a minimal MCP (Model Context Protocol) server for testing the AIGRAV system. It uses `stdio` transport and provides a single `aigrav_ping` tool.

## Setup

1. Navigate to the server directory:
   ```cmd
   cd C:\AIGRAV\MCP\TestServer
   ```

2. Create a local Python virtual environment to avoid global installations:
   ```cmd
   python -m venv venv
   ```

3. Activate the virtual environment:
   ```cmd
   venv\Scripts\activate
   ```

4. Install the required dependencies:
   ```cmd
   pip install -r requirements.txt
   ```

## Running the Server

Because this server uses the `stdio` transport, it is designed to be spawned directly by an MCP client (such as an AI agent system) which will communicate with it via standard input and output.

To test that it runs without errors, you can execute:
```cmd
python server.py
```
*(Note: It will sit and wait for JSON-RPC messages on stdin. You can press `Ctrl+C` to exit.)*
