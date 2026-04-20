## 👥 Integrantes 
-112057 Franco Domínguez 
-412258 Sakae Nakaganeku Grados
-412695 brenda Gutierrez
-106474 Gonzalo Mercado
-407944 Alexis Joel Caiguara Ramírez

# 💬 Chat App NoSQL - Sistema de Mensajería en Tiempo Real

## 📌 Descripción
Este proyecto consiste en un sistema de mensajería instantánea en tiempo real que implementa un ecosistema social completo, combinando comunicación entre usuarios, descubrimiento de comunidades y automatización mediante bots.

A diferencia de un chat tradicional, la aplicación integra interacción pública y privada dentro de una misma plataforma.

---

## 🚀 Funcionalidades principales

### 💬 Mensajería
- Chat privado entre usuarios (tipo WhatsApp)
- Chat global en tiempo real (espacio público)
- Envío y recepción de mensajes instantáneos

### 🌍 Interacción social
- Descubrimiento de usuarios sin necesidad de agregarlos previamente
- Interacción abierta mediante chat global

### 🤝 Sistema de grupos
- Creación de comunidades temáticas
- Unión y participación en grupos
- Promoción de grupos dentro del chat global

### 🤖 Bots y automatización
- Bot de búsqueda con lenguaje natural  
  Ej: *"grupos de programación"* → devuelve resultados relacionados
- Bots de contenido (libros, música, enlaces, etc.)

---

## 🧠 Tecnologías utilizadas

- Node.js
- Express
- MongoDB (Base de datos NoSQL)
- Redis (gestión de estados y rendimiento)
- Socket.io (comunicación en tiempo real)

---

## 🗄️ Modelo de datos (ejemplo)

```json
{
  "usuario": "id_usuario",
  "mensaje": "Hola mundo",
  "fecha": "2026-04-19",
  "chat": "global",
  "estado": "enviado"
}
