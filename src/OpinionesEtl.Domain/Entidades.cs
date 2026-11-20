namespace OpinionesEtl.Domain;

// Registros tal como salen de cada fuente, ya validados en formato y tipos. No dependen de ninguna tecnología.
public sealed record ClienteExtraido(string IdCliente, string Nombre, string Email);

public sealed record ProductoExtraido(string IdProducto, string Nombre, string Categoria);

public sealed record EncuestaExtraida(
    string IdOpinion, string IdCliente, string IdProducto, DateOnly Fecha,
    string Comentario, string Clasificacion, int PuntajeSatisfaccion, string Fuente);

public sealed record ResenaExtraida(
    string IdResena, string IdCliente, string IdProducto, DateOnly Fecha, string Comentario, int Rating);

public sealed record ComentarioSocialExtraido(
    string IdComentario, string? IdCliente, string IdProducto, string Canal, DateOnly Fecha, string Comentario);
