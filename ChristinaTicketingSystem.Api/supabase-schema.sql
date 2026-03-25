-- ============================================================
-- Christina Ticketing System - Supabase Schema
-- Run this in: Supabase Dashboard > SQL Editor > New Query
-- ============================================================

-- Users table
create table if not exists users (
  id serial primary key,
  username varchar(50) not null unique,
  display_name varchar(100) not null,
  role varchar(20) not null default 'Customer',
  password_hash text not null,
  created_at_utc timestamptz not null default now()
);

-- Tickets table
create table if not exists tickets (
  id serial primary key,
  title varchar(100) not null,
  description varchar(1000) not null,
  category varchar(50) not null,
  created_by_username text not null,
  created_by_display_name text not null,
  created_by_role text not null default 'Customer',
  status int not null default 0,
  priority int not null default 1,
  created_date timestamptz not null default now(),
  due_date timestamptz null,
  assigned_to varchar(100) null,
  overview varchar(1000) null,
  review_notes varchar(1000) null,
  attachment_file_name text null,
  attachment_stored_file_name text null,
  attachment_content_type text null,
  attachment_relative_path text null
);

-- Ticket comments table
create table if not exists ticket_comments (
  id serial primary key,
  ticket_id int not null references tickets(id) on delete cascade,
  author_name varchar(100) not null,
  message varchar(500) not null,
  created_date timestamptz not null default now()
);

-- Disable RLS so the service role key can read/write freely
alter table users disable row level security;
alter table tickets disable row level security;
alter table ticket_comments disable row level security;
