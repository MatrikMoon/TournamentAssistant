New-Item -Path 'JS' -ItemType Directory 
protoc -I="../Protobuf" "../Protobuf/*.proto" --js_out="JS"