// ─────────────────────────────────────────────────────────────────────────────
//  Damas Inglesas — consola + MariaDB
//  Ajusta la variable CONN con tu contraseña de MariaDB antes de ejecutar.
// ─────────────────────────────────────────────────────────────────────────────
using MySqlConnector;
using System.Text.Json;

const string CONN = "Server=localhost;Port=3306;Database=damas;User=root;Password=;";

var bd = new BD(CONN);
try { bd.Init(); }
catch (Exception ex)
{
    Console.WriteLine($"\nError al conectar con MariaDB:\n{ex.Message}");
    Console.WriteLine($"\nRevisa la variable CONN en Program.cs\nValor actual: {CONN}");
    Console.ReadKey(); return;
}

// ── Menú ──────────────────────────────────────────────────────────────────────
while (true)
{
    Console.Clear();
    Console.WriteLine("╔══════════════════════════╗");
    Console.WriteLine("║    DAMAS INGLESAS        ║");
    Console.WriteLine("╚══════════════════════════╝");
    Console.WriteLine("1. Nueva partida");
    Console.WriteLine("2. Reproducir partida guardada");
    Console.WriteLine("3. Historial");
    Console.WriteLine("4. Salir\n");
    Console.Write("Opción: ");

    switch (Console.ReadLine()?.Trim())
    {
        case "1": new Juego(bd).Jugar(); break;
        case "2": ReproducirMenu(bd);    break;
        case "3": Historial(bd); Console.WriteLine("\n[tecla]"); Console.ReadKey(); break;
        case "4": return;
    }
}

static void Historial(BD bd)
{
    Console.Clear();
    Console.WriteLine($"{"ID",-5} {"Fecha",-18} {"Estado",-15} {"Ganador",-8} Mov");
    Console.WriteLine(new string('─', 52));
    foreach (var p in bd.Partidas())
        Console.WriteLine($"{p.Id,-5} {p.Fecha,-18:dd/MM/yy HH:mm} {p.Estado,-15} {p.Ganador ?? "—",-8} {p.Movs}");
}

static void ReproducirMenu(BD bd)
{
    Historial(bd);
    Console.Write("\nID a reproducir (0 cancela): ");
    if (int.TryParse(Console.ReadLine(), out int id) && id > 0)
        new Replay(bd).Ejecutar(id);
}

// ══════════════════════════════════════════════════════════════════════════════
//  TABLERO
//  Valores: 0=vacía  1=roja  2=Reina roja  3=negra  4=Reina negra
//  Casillas oscuras (jugables): (fila + col) % 2 == 0
//  Fila 0 = arriba (lado negro)   Fila 7 = abajo (lado rojo)
// ══════════════════════════════════════════════════════════════════════════════
class Tablero
{
    public int[,] C = new int[8, 8];

    public static bool EsRojo(int p)  => p is 1 or 2;
    public static bool EsNegro(int p) => p is 3 or 4;
    public static bool Oscura(int r, int c) => (r + c) % 2 == 0;

    public Tablero Clonar() { var t = new Tablero(); Array.Copy(C, t.C, 64); return t; }

    public void Inicializar()
    {
        Array.Clear(C, 0, 64);
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                if (!Oscura(r, c)) continue;
                if (r < 3) C[r, c] = 3;   // negras arriba
                if (r > 4) C[r, c] = 1;   // rojas abajo
            }
    }

    public void Mostrar()
    {
        Console.Clear();
        Console.WriteLine("      a   b   c   d   e   f   g   h");
        Console.WriteLine("    ┌───┬───┬───┬───┬───┬───┬───┬───┐");
        for (int r = 0; r < 8; r++)
        {
            Console.Write($"  {8 - r} │");
            for (int c = 0; c < 8; c++)
            {
                int p = C[r, c];
                if (!Oscura(r, c))                                       // casilla clara
                { Console.BackgroundColor = ConsoleColor.DarkGray; Console.Write("   "); Console.ResetColor(); }
                else
                {
                    string s = p switch { 1 => " r ", 2 => "[R]", 3 => " b ", 4 => "[B]", _ => "   " };
                    if (EsRojo(p))  Console.ForegroundColor = ConsoleColor.Red;
                    if (EsNegro(p)) Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(s); Console.ResetColor();
                }
                Console.Write("│");
            }
            Console.WriteLine($" {8 - r}");
            if (r < 7) Console.WriteLine("    ├───┼───┼───┼───┼───┼───┼───┼───┤");
        }
        Console.WriteLine("    └───┴───┴───┴───┴───┴───┴───┴───┘");
        Console.WriteLine("      a   b   c   d   e   f   g   h");
        Console.WriteLine("\n  r/b = pieza simple    [R]/[B] = reina\n");
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  REGLAS  — captura obligatoria, multi-salto, coronación
// ══════════════════════════════════════════════════════════════════════════════
record Mov(int Fr, int Fc, int Tr, int Tc, List<(int, int)> Caps);

static class Reglas
{
    // Devuelve movimientos válidos. Si hay capturas disponibles, solo devuelve capturas.
    public static List<Mov> Validos(Tablero t, bool rojo)
    {
        var caps = new List<Mov>(); var norm = new List<Mov>();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                int p = t.C[r, c];
                if (rojo  && !Tablero.EsRojo(p))  continue;
                if (!rojo && !Tablero.EsNegro(p)) continue;
                foreach (var m in PiezaMovs(t, r, c))
                    (m.Caps.Count > 0 ? caps : norm).Add(m);
            }
        return caps.Count > 0 ? caps : norm;
    }

    public static Tablero Aplicar(Tablero t, Mov m)
    {
        var n = t.Clonar();
        int p = n.C[m.Fr, m.Fc];
        n.C[m.Fr, m.Fc] = 0;
        foreach (var (cr, cc) in m.Caps) n.C[cr, cc] = 0;
        bool corona = (p == 1 && m.Tr == 0) || (p == 3 && m.Tr == 7);
        n.C[m.Tr, m.Tc] = corona ? (Tablero.EsRojo(p) ? 2 : 4) : p;
        return n;
    }

    static List<Mov> PiezaMovs(Tablero t, int r, int c)
    {
        int p = t.C[r, c];
        var caps = new List<Mov>();
        BuscarCapturas(t, p, r, c, r, c, new(), new(), caps);
        if (caps.Count > 0) return caps;

        var norm = new List<Mov>();
        foreach (var (dr, dc) in Dirs(p))
        {
            int nr = r + dr, nc = c + dc;
            if (Ok(nr, nc) && t.C[nr, nc] == 0)
                norm.Add(new Mov(r, c, nr, nc, new()));
        }
        return norm;
    }

    static void BuscarCapturas(Tablero t, int p, int or_, int oc,
        int cr, int cc, List<(int, int)> vis, HashSet<(int, int)> cap, List<Mov> res)
    {
        bool found = false;
        foreach (var (dr, dc) in Dirs(p))
        {
            int mr = cr + dr, mc = cc + dc, lr = cr + 2 * dr, lc = cc + 2 * dc;
            if (!Ok(lr, lc) || cap.Contains((mr, mc))) continue;
            if (!Enemigo(p, t.C[mr, mc]) || t.C[lr, lc] != 0) continue;
            if (vis.Contains((lr, lc))) continue;

            found = true;
            var nv = new List<(int, int)>(vis) { (lr, lc) };
            var nc2 = new HashSet<(int, int)>(cap) { (mr, mc) };
            bool corona = (p == 1 && lr == 0) || (p == 3 && lr == 7);
            if (corona) res.Add(new Mov(or_, oc, lr, lc, nc2.ToList()));
            else        BuscarCapturas(t, p, or_, oc, lr, lc, nv, nc2, res);
        }
        if (!found && vis.Count > 0)
            res.Add(new Mov(or_, oc, vis[^1].Item1, vis[^1].Item2, cap.ToList()));
    }

    static (int, int)[] Dirs(int p) => p switch
    {
        1 => new[] { (-1, -1), (-1, +1) },             // roja simple: sube
        3 => new[] { (+1, -1), (+1, +1) },             // negra simple: baja
        _ => new[] { (-1, -1), (-1, +1), (+1, -1), (+1, +1) }  // reina
    };

    static bool Enemigo(int p, int e) =>
        (Tablero.EsRojo(p) && Tablero.EsNegro(e)) || (Tablero.EsNegro(p) && Tablero.EsRojo(e));

    static bool Ok(int r, int c) => (uint)r < 8 && (uint)c < 8;
}

// ══════════════════════════════════════════════════════════════════════════════
//  JUEGO
// ══════════════════════════════════════════════════════════════════════════════
class Juego
{
    private readonly BD bd;
    public Juego(BD bd) { this.bd = bd; }

    public void Jugar()
    {
        var t = new Tablero(); t.Inicializar();
        bool rojo = true; int id = bd.CrearPartida(); int n = 0;

        Console.Clear();
        Console.WriteLine($"Partida #{id}  |  ROJAS van primero");
        Console.WriteLine("Formato: c3-d4   'ayuda' = ver opciones   'salir' = abandonar");
        Console.ReadKey();

        while (true)
        {
            var validos = Reglas.Validos(t, rojo);
            if (validos.Count == 0)
            {
                string gan = rojo ? "Negro" : "Rojo";
                bd.Estado(id, rojo ? "negras_ganan" : "rojas_ganan", gan);
                t.Mostrar(); Console.WriteLine($"¡Fin!  Ganan las piezas {gan.ToUpper()}.");
                Console.ReadKey(); return;
            }

            t.Mostrar();
            bool hayCap = validos.Any(m => m.Caps.Count > 0);
            Console.ForegroundColor = rojo ? ConsoleColor.Red : ConsoleColor.Cyan;
            Console.Write($"Turno #{n + 1}  {(rojo ? "ROJO" : "NEGRO")}");
            Console.ResetColor();
            if (hayCap) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write("  ⚠ captura obligatoria"); Console.ResetColor(); }
            Console.Write("\nMovimiento: ");

            string? inp = Console.ReadLine()?.Trim().ToLower();
            if (inp is "salir" or "q") { bd.Estado(id, "abandonada", rojo ? "Negro" : "Rojo"); return; }
            if (inp is "ayuda" or "?") { Ayuda(t, validos); continue; }

            var mov = Parsear(inp, validos);
            if (mov == null) { Console.WriteLine("Inválido."); Thread.Sleep(1100); continue; }

            bool corona = (t.C[mov.Fr, mov.Fc] == 1 && mov.Tr == 0)
                       || (t.C[mov.Fr, mov.Fc] == 3 && mov.Tr == 7);
            n++;
            t = Reglas.Aplicar(t, mov);
            bd.GuardarMov(id, n, rojo ? "Rojo" : "Negro", mov, corona);
            if (corona) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine("¡Coronación!"); Console.ResetColor(); Thread.Sleep(1200); }
            rojo = !rojo;
        }
    }

    static void Ayuda(Tablero t, List<Mov> validos)
    {
        t.Mostrar();
        Console.WriteLine("Movimientos válidos:");
        foreach (var m in validos)
            Console.WriteLine($"  {P(m.Fr, m.Fc)}-{P(m.Tr, m.Tc)}" +
                              (m.Caps.Count > 0 ? $"  (captura {m.Caps.Count})" : ""));
        Console.WriteLine("[tecla]"); Console.ReadKey();
    }

    static Mov? Parsear(string? s, List<Mov> validos)
    {
        if (s == null) return null;
        var pts = s.Replace(" ", "-").Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (pts.Length < 2) return null;
        var a = Pos(pts[0]); var b = Pos(pts[1]);
        if (a == null || b == null) return null;
        return validos.FirstOrDefault(m => m.Fr == a.Value.r && m.Fc == a.Value.c
                                        && m.Tr == b.Value.r && m.Tc == b.Value.c);
    }

    static (int r, int c)? Pos(string s)
    {
        if (s.Length < 2 || s[0] < 'a' || s[0] > 'h') return null;
        if (!int.TryParse(s[1..], out int f) || f < 1 || f > 8) return null;
        return (8 - f, s[0] - 'a');
    }

    public static string P(int r, int c) => $"{(char)('a' + c)}{8 - r}";
}

// ══════════════════════════════════════════════════════════════════════════════
//  REPLAY
// ══════════════════════════════════════════════════════════════════════════════
class Replay
{
    private readonly BD bd;
    public Replay(BD bd) { this.bd = bd; }

    public void Ejecutar(int id)
    {
        var info = bd.InfoPartida(id);
        if (info == null) { Console.WriteLine("No encontrada."); Console.ReadKey(); return; }
        var movs = bd.MovsPartida(id);
        var t = new Tablero(); t.Inicializar(); t.Mostrar();
        Console.WriteLine($"Partida #{id} | {info.Estado} | {movs.Count} movimientos");
        Console.WriteLine("ENTER = avanzar   q = salir");
        Console.ReadKey();

        foreach (var (m, jugador, corona) in movs)
        {
            t.Mostrar();
            Console.ForegroundColor = jugador == "Rojo" ? ConsoleColor.Red : ConsoleColor.Cyan;
            Console.Write($"{jugador}: {Juego.P(m.Fr, m.Fc)}→{Juego.P(m.Tr, m.Tc)}");
            if (m.Caps.Count > 0) Console.Write($"  captura {m.Caps.Count}");
            if (corona) Console.Write("  ¡CORONACIÓN!");
            Console.ResetColor();
            Console.Write("  [ENTER/q]: ");
            if (Console.ReadKey().KeyChar == 'q') return;
            t = Reglas.Aplicar(t, m);
        }
        t.Mostrar();
        Console.WriteLine($"\nResultado: {info.Estado}  Ganador: {info.Ganador ?? "—"}");
        Console.ReadKey();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  BASE DE DATOS (MariaDB)
// ══════════════════════════════════════════════════════════════════════════════
class BD
{
    private readonly string conn;
    public BD(string conn) { this.conn = conn; }

    public void Init()
    {
        using var c = Open();
        Run(c, @"CREATE TABLE IF NOT EXISTS juegos (
            id INT AUTO_INCREMENT PRIMARY KEY,
            creado_en DATETIME DEFAULT CURRENT_TIMESTAMP,
            estado VARCHAR(20) DEFAULT 'en_progreso',
            ganador VARCHAR(10) NULL) ENGINE=InnoDB;");
        Run(c, @"CREATE TABLE IF NOT EXISTS movimientos (
            id INT AUTO_INCREMENT PRIMARY KEY,
            juego_id INT NOT NULL,
            num INT NOT NULL,
            jugador VARCHAR(10) NOT NULL,
            fr TINYINT, fc TINYINT, tr TINYINT, tc TINYINT,
            capturas TEXT NULL,
            corona BOOL DEFAULT FALSE,
            FOREIGN KEY(juego_id) REFERENCES juegos(id)) ENGINE=InnoDB;");
    }

    public int CrearPartida()
    {
        using var c = Open();
        using var cmd = new MySqlCommand("INSERT INTO juegos() VALUES(); SELECT LAST_INSERT_ID();", c);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void Estado(int id, string estado, string? ganador = null)
    {
        using var c = Open();
        using var cmd = new MySqlCommand("UPDATE juegos SET estado=@e,ganador=@g WHERE id=@id", c);
        cmd.Parameters.AddWithValue("@e", estado);
        cmd.Parameters.AddWithValue("@g", (object?)ganador ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void GuardarMov(int gid, int num, string jugador, Mov m, bool corona)
    {
        string? caps = m.Caps.Count > 0
            ? JsonSerializer.Serialize(m.Caps.Select(x => new { r = x.Item1, c = x.Item2 }))
            : null;
        using var c = Open();
        using var cmd = new MySqlCommand(
            "INSERT INTO movimientos(juego_id,num,jugador,fr,fc,tr,tc,capturas,corona)" +
            " VALUES(@g,@n,@j,@fr,@fc,@tr,@tc,@caps,@cor)", c);
        cmd.Parameters.AddWithValue("@g",    gid);
        cmd.Parameters.AddWithValue("@n",    num);
        cmd.Parameters.AddWithValue("@j",    jugador);
        cmd.Parameters.AddWithValue("@fr",   m.Fr);  cmd.Parameters.AddWithValue("@fc", m.Fc);
        cmd.Parameters.AddWithValue("@tr",   m.Tr);  cmd.Parameters.AddWithValue("@tc", m.Tc);
        cmd.Parameters.AddWithValue("@caps", (object?)caps ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cor",  corona);
        cmd.ExecuteNonQuery();
    }

    public record Info(int Id, DateTime Fecha, string Estado, string? Ganador, int Movs);

    public List<Info> Partidas()
    {
        using var c = Open();
        using var cmd = new MySqlCommand(@"SELECT j.id,j.creado_en,j.estado,j.ganador,COUNT(m.id)
            FROM juegos j LEFT JOIN movimientos m ON m.juego_id=j.id
            GROUP BY j.id ORDER BY j.id DESC", c);
        var list = new List<Info>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new(r.GetInt32(0), r.GetDateTime(1), r.GetString(2),
                         r.IsDBNull(3) ? null : r.GetString(3), r.GetInt32(4)));
        return list;
    }

    public Info? InfoPartida(int id)
    {
        using var c = Open();
        using var cmd = new MySqlCommand("SELECT id,creado_en,estado,ganador FROM juegos WHERE id=@id", c);
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new(r.GetInt32(0), r.GetDateTime(1), r.GetString(2),
                   r.IsDBNull(3) ? null : r.GetString(3), 0);
    }

    public List<(Mov, string, bool)> MovsPartida(int id)
    {
        using var c = Open();
        using var cmd = new MySqlCommand(
            "SELECT fr,fc,tr,tc,capturas,corona,jugador FROM movimientos WHERE juego_id=@id ORDER BY num", c);
        cmd.Parameters.AddWithValue("@id", id);
        var list = new List<(Mov, string, bool)>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var caps = new List<(int, int)>();
            if (!r.IsDBNull(4))
            {
                using var doc = JsonDocument.Parse(r.GetString(4));
                foreach (var e in doc.RootElement.EnumerateArray())
                    caps.Add((e.GetProperty("r").GetInt32(), e.GetProperty("c").GetInt32()));
            }
            list.Add((new Mov(r.GetByte(0), r.GetByte(1), r.GetByte(2), r.GetByte(3), caps),
                      r.GetString(6), r.GetBoolean(5)));
        }
        return list;
    }

    MySqlConnection Open() { var c = new MySqlConnection(conn); c.Open(); return c; }
    static void Run(MySqlConnection c, string sql) { using var cmd = new MySqlCommand(sql, c); cmd.ExecuteNonQuery(); }
}
