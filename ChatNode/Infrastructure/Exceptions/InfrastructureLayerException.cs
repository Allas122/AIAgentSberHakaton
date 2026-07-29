namespace ChatNode.Infrastructure.Exceptions;

public class InfrastructureLayerException(string message) : Exception(message);

public class NotFoundException(string message) : InfrastructureLayerException(message);

public class CreationException(string message) : InfrastructureLayerException(message);